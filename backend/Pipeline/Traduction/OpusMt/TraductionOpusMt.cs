using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Traduction.OpusMt
{
    /// <summary>
    /// Traduit avec OPUS-MT exporté en ONNX, entièrement en local.
    /// </summary>
    /// <remarks>
    /// Le modèle est un transformeur encodeur-décodeur. On encode la phrase anglaise
    /// en une passe, puis on produit le français <b>un jeton à la fois</b>, chaque
    /// jeton produit étant réinjecté pour obtenir le suivant. Le cache du décodeur
    /// évite de tout recalculer à chaque tour.
    /// <para>
    /// Le piège de la tokenisation, mesuré sur les fichiers réels : <c>source.spm</c>
    /// porte 32 000 pièces avec ses propres identifiants, alors que le modèle en
    /// attend 59 514 numérotés autrement — aucune correspondance entre les deux. On
    /// tokenise donc en <em>pièces</em> avec SentencePiece, puis on traduit ces pièces
    /// en identifiants par <c>vocab.json</c>. Utiliser directement les identifiants de
    /// SentencePiece donnerait du charabia.
    /// </para>
    /// <para>
    /// Le décodage est glouton : à chaque tour on prend le jeton le plus probable. La
    /// recherche en faisceau que recommande le modèle donnerait un français un peu
    /// meilleur, pour quatre fois le travail — et la traduction n'est ici qu'un
    /// brouillon que l'utilisateur corrige.
    /// </para>
    /// </remarks>
    public partial class TraductionOpusMt : ITraducteur
    {
        #region Constantes

        private const string FichierEncodeur = "encoder_model.onnx";
        private const string FichierDecodeur = "decoder_model_merged.onnx";
        private const string FichierTokeniseur = "source.spm";
        private const string FichierVocabulaire = "vocab.json";
        private const string FichierConfiguration = "config.json";

        /// <summary>
        /// Marque de début de mot de SentencePiece, à retransformer en espace.
        /// </summary>
        private const char MarqueDeMot = '▁';

        /// <summary>
        /// Plafond absolu de jetons produits pour une phrase, quelle qu'elle soit.
        /// </summary>
        private const int PlafondDeGeneration = 512;

        /// <summary>
        /// Un texte traduit fait rarement plus du triple de sa source en jetons. Ce
        /// plafond relatif coupe court aux répétitions dégénérées, dans lesquelles ces
        /// modèles partent parfois sur une entrée abîmée — et l'OCR d'une planche en
        /// produit.
        /// </summary>
        private const int FacteurDeLongueur = 3;

        private const int MargeDeLongueur = 20;

        /// <summary>
        /// Nombre d'hypothèses menées de front par la recherche en faisceau. Quatre est
        /// ce que recommande la configuration du modèle. À un, la recherche redevient
        /// un décodage glouton : on prend le jeton le plus probable à chaque tour, sans
        /// jamais revenir dessus.
        /// </summary>
        private const int NombreDeFaisceauxParDefaut = 4;

        #endregion

        #region Attributs

        private InferenceSession encodeur;
        private InferenceSession decodeur;
        private SentencePieceTokenizer tokeniseur;
        private Dictionary<string, int> versIdentifiant;
        private string[] versPiece;
        private int identifiantInconnu;
        private int identifiantDeRemplissage;
        private int identifiantDeFin;
        private int debutDuDecodage;
        private int couches;
        private int tetes;
        private int dimensionDeTete;
        private int nombreDeFaisceaux;
        private bool libere;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Charge le modèle exporté depuis le dossier indiqué.
        /// </summary>
        /// <param name="cheminDuDossier">
        /// Dossier contenant l'export ONNX : les deux modèles, le tokeniseur, le
        /// vocabulaire et la configuration.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="cheminDuDossier"/> est vide.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Levée si l'un des fichiers de l'export manque.
        /// </exception>
        public TraductionOpusMt(string cheminDuDossier)
            : this(cheminDuDossier, NombreDeFaisceauxParDefaut)
        {
        }

        /// <summary>
        /// Charge le modèle et fixe la largeur de la recherche en faisceau.
        /// </summary>
        /// <param name="cheminDuDossier">
        /// Dossier contenant l'export ONNX : les deux modèles, le tokeniseur, le
        /// vocabulaire et la configuration.
        /// </param>
        /// <param name="nombreDeFaisceaux">
        /// Nombre d'hypothèses menées de front. Un seul faisceau revient à un décodage
        /// glouton, plus rapide mais moins bon ; le coût croît proportionnellement.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="cheminDuDossier"/> est vide.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Levée si <paramref name="nombreDeFaisceaux"/> est inférieur à un.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Levée si l'un des fichiers de l'export manque.
        /// </exception>
        public TraductionOpusMt(string cheminDuDossier, int nombreDeFaisceaux)
        {
            if (string.IsNullOrWhiteSpace(cheminDuDossier))
            {
                throw new ArgumentException(
                    "Le dossier du modèle doit être renseigné.", nameof(cheminDuDossier));
            }

            if (nombreDeFaisceaux < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nombreDeFaisceaux), nombreDeFaisceaux,
                    "Il faut au moins un faisceau pour produire quelque chose.");
            }

            this.nombreDeFaisceaux = nombreDeFaisceaux;
            libere = false;
            versIdentifiant = new Dictionary<string, int>();
            versPiece = Array.Empty<string>();

            LireLaConfiguration(Exiger(cheminDuDossier, FichierConfiguration));
            LireLeVocabulaire(Exiger(cheminDuDossier, FichierVocabulaire));

            using FileStream modele = File.OpenRead(Exiger(cheminDuDossier, FichierTokeniseur));

            // Ni début ni fin de phrase : Marian n'attend qu'un « </s> » final, qu'on
            // ajoute nous-mêmes après la traduction des pièces en identifiants.
            tokeniseur = SentencePieceTokenizer.Create(
                modele, addBeginningOfSentence: false, addEndOfSentence: false);

            encodeur = new InferenceSession(Exiger(cheminDuDossier, FichierEncodeur));
            decodeur = new InferenceSession(Exiger(cheminDuDossier, FichierDecodeur));
        }

        #endregion

        #region Méthodes

        /// <inheritdoc />
        public Task<Planche> TraduireAsync(
            Planche planche, CancellationToken jetonAnnulation = default)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            ObjectDisposedException.ThrowIf(libere, this);

            return Task.Run(
                () =>
                {
                    foreach (ZoneDeTexte zone in planche.Zones)
                    {
                        jetonAnnulation.ThrowIfCancellationRequested();

                        // Une zone sans texte d'origine n'a rien à traduire ; l'envoyer
                        // au modèle ne produirait que du bruit.
                        if (!string.IsNullOrWhiteSpace(zone.TexteOriginal))
                        {
                            zone.TexteTraduit = Traduire(zone.TexteOriginal);
                        }
                    }

                    return planche.AvecZones(planche.Zones);
                },
                jetonAnnulation);
        }

        /// <summary>
        /// Libère les deux sessions ONNX et la mémoire native qu'elles retiennent.
        /// </summary>
        public void Dispose()
        {
            if (libere)
            {
                return;
            }

            encodeur.Dispose();
            decodeur.Dispose();
            libere = true;

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Méthodes privées

        private string? Traduire(string texte)
        {
            long[] entree = VersIdentifiants(texte);

            if (entree.Length <= 1)
            {
                // Rien que le « </s> » : il n'y avait pas de texte exploitable.
                return null;
            }

            List<int> produits = Decoder(Encoder(entree), entree.Length);

            return produits.Count == 0 ? null : VersTexte(produits);
        }

        private static string NormaliserLaCasse(string texte)
        {
            // Mesuré sur trois phrases d'essai, tout en majuscules contre la même en
            // casse normale : « one of our instructors suddenly quit » ressortait en
            // « a été à l'origine » avec un mot inventé, « replacements on short
            // notice » en « correspondances sur cet avis », et « go off » en « te
            // déshabiller ». Les trois sont justes en casse normale. Le modèle n'a
            // jamais vu de texte crié, et une planche de manga n'est faite que de ça.
            if (texte.Any(char.IsLower) || !texte.Any(char.IsUpper))
            {
                return texte;
            }

            char[] lettres = texte.ToLowerInvariant().ToCharArray();

            for (int rang = 0; rang < lettres.Length; rang++)
            {
                if (char.IsLetter(lettres[rang]))
                {
                    lettres[rang] = char.ToUpperInvariant(lettres[rang]);
                    break;
                }
            }

            // « I » isolé est un pronom, pas une lettre à minusculer.
            return PronomIsole().Replace(new string(lettres), "I");
        }

        [GeneratedRegex(@"\bi\b")]
        private static partial Regex PronomIsole();

        private long[] VersIdentifiants(string texte)
        {
            IReadOnlyList<EncodedToken> pieces =
                tokeniseur.EncodeToTokens(NormaliserLaCasse(texte), out string? _);

            long[] identifiants = new long[pieces.Count + 1];

            for (int rang = 0; rang < pieces.Count; rang++)
            {
                identifiants[rang] = versIdentifiant.TryGetValue(pieces[rang].Value, out int trouve)
                    ? trouve
                    : identifiantInconnu;
            }

            identifiants[pieces.Count] = identifiantDeFin;

            return identifiants;
        }

        private DenseTensor<float> Encoder(long[] entree)
        {
            DenseTensor<long> identifiants = new DenseTensor<long>(new[] { 1, entree.Length });
            DenseTensor<long> masque = new DenseTensor<long>(new[] { 1, entree.Length });

            for (int rang = 0; rang < entree.Length; rang++)
            {
                identifiants[0, rang] = entree[rang];
                masque[0, rang] = 1;
            }

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> sortie = encodeur.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("input_ids", identifiants),
                NamedOnnxValue.CreateFromTensor("attention_mask", masque)
            });

            // Recopié avant que la collection ne se libère : le décodeur s'en sert à
            // chaque tour, bien après la fin de cet appel.
            return Copier(sortie.First().AsTensor<float>());
        }

        private List<int> Decoder(DenseTensor<float> etat, int longueurSource)
        {
            DenseTensor<long> masqueEncodeur = new DenseTensor<long>(new[] { 1, longueurSource });

            for (int rang = 0; rang < longueurSource; rang++)
            {
                masqueEncodeur[0, rang] = 1;
            }

            // Le cache de l'encodeur ne dépend que de la phrase source : il se calcule
            // au premier tour et se partage ensuite entre tous les faisceaux.
            DenseTensor<float>[] cacheEncodeur = CacheVide(couches * 2);

            List<FaisceauDeDecodage> faisceaux = new List<FaisceauDeDecodage>
            {
                new FaisceauDeDecodage(
                    new List<int> { debutDuDecodage }, 0, CacheVide(couches * 2))
            };

            List<FaisceauDeDecodage> termines = new List<FaisceauDeDecodage>();

            int plafond = Math.Min(
                PlafondDeGeneration, (longueurSource * FacteurDeLongueur) + MargeDeLongueur);

            for (int tour = 0; tour < plafond && faisceaux.Count > 0; tour++)
            {
                List<FaisceauDeDecodage> candidats = new List<FaisceauDeDecodage>();

                foreach (FaisceauDeDecodage faisceau in faisceaux)
                {
                    DenseTensor<float>[] cacheSuivant = new DenseTensor<float>[couches * 2];

                    float[] vraisemblances = Avancer(
                        faisceau, etat, masqueEncodeur, cacheEncodeur, tour == 0, cacheSuivant);

                    // Tous les prolongements d'un même faisceau partagent son cache :
                    // ils ont la même histoire jusqu'ici, ils ne divergent qu'au jeton
                    // qui sera lu au tour suivant.
                    foreach ((int Jeton, double LogProb) candidat in MeilleursJetons(vraisemblances))
                    {
                        candidats.Add(new FaisceauDeDecodage(
                            new List<int>(faisceau.Jetons) { candidat.Jeton },
                            faisceau.Score + candidat.LogProb,
                            cacheSuivant));
                    }
                }

                List<FaisceauDeDecodage> suivants = new List<FaisceauDeDecodage>();

                foreach (FaisceauDeDecodage candidat in candidats.OrderByDescending(c => c.Score))
                {
                    if (candidat.Dernier == identifiantDeFin)
                    {
                        termines.Add(candidat);
                    }
                    else if (suivants.Count < nombreDeFaisceaux)
                    {
                        suivants.Add(candidat);
                    }
                }

                // Autant d'hypothèses achevées que de faisceaux : continuer ne pourrait
                // plus produire mieux que ce qu'on a déjà.
                if (termines.Count >= nombreDeFaisceaux)
                {
                    break;
                }

                faisceaux = suivants;
            }

            return Retenir(termines.Count > 0 ? termines : faisceaux);
        }

        private List<int> Retenir(List<FaisceauDeDecodage> hypotheses)
        {
            if (hypotheses.Count == 0)
            {
                return new List<int>();
            }

            FaisceauDeDecodage meilleure = hypotheses
                .OrderByDescending(hypothese => hypothese.ScoreNormalise())
                .First();

            // Le premier jeton n'est que l'amorce du décodage, le dernier la marque de
            // fin : ni l'un ni l'autre n'appartient à la traduction.
            return meilleure.Jetons
                .Skip(1)
                .Where(jeton => jeton != identifiantDeFin)
                .ToList();
        }

        private float[] Avancer(
            FaisceauDeDecodage faisceau,
            DenseTensor<float> etat,
            DenseTensor<long> masqueEncodeur,
            DenseTensor<float>[] cacheEncodeur,
            bool premier,
            DenseTensor<float>[] cacheSuivant)
        {
            List<NamedOnnxValue> entrees = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("encoder_attention_mask", masqueEncodeur),
                NamedOnnxValue.CreateFromTensor("input_ids", UnJeton(faisceau.Dernier)),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", etat),
                NamedOnnxValue.CreateFromTensor("use_cache_branch", Drapeau(!premier))
            };

            for (int couche = 0; couche < couches; couche++)
            {
                entrees.Add(NamedOnnxValue.CreateFromTensor(
                    $"past_key_values.{couche}.decoder.key", faisceau.Cache[couche * 2]));
                entrees.Add(NamedOnnxValue.CreateFromTensor(
                    $"past_key_values.{couche}.decoder.value", faisceau.Cache[(couche * 2) + 1]));
                entrees.Add(NamedOnnxValue.CreateFromTensor(
                    $"past_key_values.{couche}.encoder.key", cacheEncodeur[couche * 2]));
                entrees.Add(NamedOnnxValue.CreateFromTensor(
                    $"past_key_values.{couche}.encoder.value", cacheEncodeur[(couche * 2) + 1]));
            }

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> sortie =
                decodeur.Run(entrees);

            Dictionary<string, DisposableNamedOnnxValue> parNom =
                sortie.ToDictionary(valeur => valeur.Name);

            for (int couche = 0; couche < couches; couche++)
            {
                cacheSuivant[couche * 2] =
                    Copier(parNom[$"present.{couche}.decoder.key"].AsTensor<float>());
                cacheSuivant[(couche * 2) + 1] =
                    Copier(parNom[$"present.{couche}.decoder.value"].AsTensor<float>());

                if (premier)
                {
                    cacheEncodeur[couche * 2] =
                        Copier(parNom[$"present.{couche}.encoder.key"].AsTensor<float>());
                    cacheEncodeur[(couche * 2) + 1] =
                        Copier(parNom[$"present.{couche}.encoder.value"].AsTensor<float>());
                }
            }

            return LogVraisemblances(parNom["logits"].AsTensor<float>());
        }

        private float[] LogVraisemblances(Tensor<float> logits)
        {
            // La sortie vaut [1, longueur, vocabulaire] ; on ne produit qu'un jeton par
            // tour, donc la dernière position est la seule qui compte.
            int vocabulaire = logits.Dimensions[2];
            int derniere = logits.Dimensions[1] - 1;

            float[] valeurs = new float[vocabulaire];
            float maximum = float.NegativeInfinity;

            for (int jeton = 0; jeton < vocabulaire; jeton++)
            {
                valeurs[jeton] = logits[0, derniere, jeton];

                if (valeurs[jeton] > maximum)
                {
                    maximum = valeurs[jeton];
                }
            }

            // Les scores bruts du modèle ne sont pas comparables d'un tour à l'autre :
            // on les ramène en logarithmes de probabilité, qui eux s'additionnent le
            // long d'une hypothèse. Le retrait du maximum évite que l'exponentielle
            // ne déborde.
            double somme = 0;

            for (int jeton = 0; jeton < vocabulaire; jeton++)
            {
                somme += Math.Exp(valeurs[jeton] - maximum);
            }

            float decalage = maximum + (float)Math.Log(somme);

            for (int jeton = 0; jeton < vocabulaire; jeton++)
            {
                valeurs[jeton] -= decalage;
            }

            // Le remplissage sert d'amorce au décodage mais ne doit jamais être
            // produit : le modèle le déclare lui-même comme mot interdit.
            valeurs[identifiantDeRemplissage] = float.NegativeInfinity;

            return valeurs;
        }

        private List<(int Jeton, double LogProb)> MeilleursJetons(float[] vraisemblances)
        {
            // Sélection partielle plutôt qu'un tri : on cherche quatre valeurs parmi
            // près de soixante mille, à chaque tour et pour chaque faisceau.
            List<(int Jeton, double LogProb)> meilleurs =
                new List<(int Jeton, double LogProb)>(nombreDeFaisceaux);

            for (int jeton = 0; jeton < vraisemblances.Length; jeton++)
            {
                float valeur = vraisemblances[jeton];

                if (meilleurs.Count < nombreDeFaisceaux)
                {
                    meilleurs.Add((jeton, valeur));
                }
                else if (valeur > meilleurs[nombreDeFaisceaux - 1].LogProb)
                {
                    meilleurs[nombreDeFaisceaux - 1] = (jeton, valeur);
                }
                else
                {
                    continue;
                }

                meilleurs.Sort((une, autre) => autre.LogProb.CompareTo(une.LogProb));
            }

            return meilleurs;
        }

        private string VersTexte(List<int> produits)
        {
            string assemble = string.Concat(produits
                .Where(identifiant => identifiant >= 0 && identifiant < versPiece.Length)
                .Select(identifiant => versPiece[identifiant]));

            return assemble.Replace(MarqueDeMot, ' ').Trim();
        }

        private DenseTensor<float>[] CacheVide(int nombre)
        {
            DenseTensor<float>[] cache = new DenseTensor<float>[nombre];

            for (int rang = 0; rang < nombre; rang++)
            {
                // Longueur nulle : au premier tour le modèle emprunte la branche sans
                // cache et ne lit pas ces tenseurs, mais il exige qu'ils soient là.
                cache[rang] = new DenseTensor<float>(new[] { 1, tetes, 0, dimensionDeTete });
            }

            return cache;
        }

        private DenseTensor<long> UnJeton(int identifiant)
        {
            DenseTensor<long> jeton = new DenseTensor<long>(new[] { 1, 1 });

            jeton[0, 0] = identifiant;

            return jeton;
        }

        private static DenseTensor<bool> Drapeau(bool valeur)
        {
            DenseTensor<bool> drapeau = new DenseTensor<bool>(new[] { 1 });

            drapeau[0] = valeur;

            return drapeau;
        }

        private static DenseTensor<float> Copier(Tensor<float> source)
        {
            // Les tenseurs rendus par ONNX Runtime pointent vers de la mémoire native
            // libérée à la fin du tour : il faut les recopier pour les réutiliser.
            DenseTensor<float> dense = source.ToDenseTensor();

            return new DenseTensor<float>(dense.Buffer.ToArray(), dense.Dimensions.ToArray());
        }

        private void LireLaConfiguration(string chemin)
        {
            using JsonDocument configuration = JsonDocument.Parse(File.ReadAllText(chemin));
            JsonElement racine = configuration.RootElement;

            identifiantDeRemplissage = Entier(racine, "pad_token_id", 59513);
            identifiantDeFin = Entier(racine, "eos_token_id", 0);
            debutDuDecodage = Entier(racine, "decoder_start_token_id", identifiantDeRemplissage);
            couches = Entier(racine, "decoder_layers", 6);
            tetes = Entier(racine, "decoder_attention_heads", 8);

            dimensionDeTete = Entier(racine, "d_model", 512) / tetes;
        }

        private void LireLeVocabulaire(string chemin)
        {
            Dictionary<string, int>? lu = JsonSerializer.Deserialize<Dictionary<string, int>>(
                File.ReadAllText(chemin));

            if (lu == null || lu.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Le vocabulaire {chemin} est vide ou illisible.");
            }

            versIdentifiant = lu;
            versPiece = new string[lu.Values.Max() + 1];

            foreach (KeyValuePair<string, int> entree in lu)
            {
                versPiece[entree.Value] = entree.Key;
            }

            for (int rang = 0; rang < versPiece.Length; rang++)
            {
                versPiece[rang] ??= string.Empty;
            }

            identifiantInconnu = versIdentifiant.TryGetValue("<unk>", out int inconnu) ? inconnu : 1;
        }

        private static int Entier(JsonElement racine, string nom, int defaut)
        {
            return racine.TryGetProperty(nom, out JsonElement valeur)
                && valeur.ValueKind == JsonValueKind.Number
                ? valeur.GetInt32()
                : defaut;
        }

        private static string Exiger(string dossier, string fichier)
        {
            string chemin = Path.Combine(dossier, fichier);

            if (!File.Exists(chemin))
            {
                throw new FileNotFoundException(
                    $"Le fichier {fichier} manque dans l'export OPUS-MT. Voir " +
                    "backend/Pipeline/Traduction/OPUS-MT.md pour le régénérer.",
                    chemin);
            }

            return chemin;
        }

        #endregion
    }
}
