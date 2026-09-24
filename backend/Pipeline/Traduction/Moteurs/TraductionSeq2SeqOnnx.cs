using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Traduction.Moteurs
{
    /// <summary>
    /// Ce que tout modèle de traduction encodeur-décodeur exporté en ONNX a en
    /// commun : la recherche en faisceau, la gestion du cache, et la traversée d'une
    /// planche.
    /// </summary>
    /// <remarks>
    /// Un modèle dérivé n'a plus qu'à dire comment il tokenise, comment il appelle
    /// ses sessions ONNX, et quels jetons spéciaux il emploie. Tout le reste — et
    /// c'est l'essentiel du code — vaut pour tous.
    /// <para>
    /// L'appel aux sessions est laissé aux classes filles parce que les exports ne se
    /// ressemblent pas : certains modèles rendent un décodeur unique piloté par un
    /// drapeau, d'autres deux décodeurs séparés. Vouloir unifier ça ici obligerait à
    /// un réglage par cas, pour un gain nul.
    /// </para>
    /// <para>
    /// Les dimensions — nombre de couches, de têtes, taille des têtes — se lisent
    /// dans <c>config.json</c> et non en dur : c'est ce qui permet à la même
    /// mécanique de servir un modèle à six couches et un autre à douze.
    /// </para>
    /// </remarks>
    public abstract class TraductionSeq2SeqOnnx : ITraducteur
    {
        #region Constantes

        /// <summary>
        /// Nom du fichier de configuration, présent dans tout export Optimum.
        /// </summary>
        protected const string FichierConfiguration = "config.json";

        /// <summary>
        /// Marque de début de mot de SentencePiece, à retransformer en espace.
        /// </summary>
        protected const char MarqueDeMot = '▁';

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
        /// Nombre d'hypothèses menées de front par la recherche en faisceau. À un, la
        /// recherche redevient un décodage glouton : on prend le jeton le plus probable
        /// à chaque tour, sans jamais revenir dessus.
        /// </summary>
        protected const int NombreDeFaisceauxParDefaut = 4;

        #endregion

        #region Attributs

        private int identifiantDeRemplissage;
        private int identifiantDeFin;
        private int debutDuDecodage;
        private int couches;
        private int tetes;
        private int dimensionDeTete;
        private int nombreDeFaisceaux;
        private bool libere;

        #endregion

        #region Propriétés

        /// <summary>
        /// Nombre de couches du décodeur, donc de blocs de cache à transporter.
        /// </summary>
        protected int Couches
        {
            get { return couches; }
        }

        /// <summary>
        /// Identifiant du jeton de fin de phrase.
        /// </summary>
        protected int IdentifiantDeFin
        {
            get { return identifiantDeFin; }
        }

        /// <summary>
        /// Identifiant du jeton de remplissage, qui ne doit jamais être produit.
        /// </summary>
        protected int IdentifiantDeRemplissage
        {
            get { return identifiantDeRemplissage; }
        }

        #endregion

        #region Constructeurs

        /// <summary>
        /// Lit la configuration du modèle et fixe la largeur de la recherche.
        /// </summary>
        /// <param name="cheminDuDossier">Dossier de l'export ONNX.</param>
        /// <param name="nombreDeFaisceaux">Nombre d'hypothèses menées de front.</param>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="cheminDuDossier"/> est vide.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Levée si <paramref name="nombreDeFaisceaux"/> est inférieur à un.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Levée si la configuration du modèle manque.
        /// </exception>
        protected TraductionSeq2SeqOnnx(string cheminDuDossier, int nombreDeFaisceaux)
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
            this.libere = false;

            LireLaConfiguration(Exiger(cheminDuDossier, FichierConfiguration));
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
        /// Libère les sessions ONNX et la mémoire native qu'elles retiennent.
        /// </summary>
        public void Dispose()
        {
            if (libere)
            {
                return;
            }

            Liberer();
            libere = true;

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Méthodes privées

        /// <summary>
        /// Traduit un texte en identifiants d'entrée pour le modèle, marqueurs compris.
        /// </summary>
        /// <param name="texte">Le texte source.</param>
        /// <returns>La suite d'identifiants à donner à l'encodeur.</returns>
        protected abstract long[] VersIdentifiants(string texte);

        /// <summary>
        /// Reconstruit le texte à partir des identifiants produits.
        /// </summary>
        /// <param name="produits">Les identifiants rendus par le décodage.</param>
        /// <returns>Le texte traduit.</returns>
        protected abstract string VersTexte(List<int> produits);

        /// <summary>
        /// Encode la phrase source en une passe.
        /// </summary>
        /// <param name="entree">Les identifiants de la phrase source.</param>
        /// <returns>L'état caché de l'encodeur, recopié pour survivre à l'appel.</returns>
        protected abstract DenseTensor<float> Encoder(long[] entree);

        /// <summary>
        /// Fait avancer un faisceau d'un jeton.
        /// </summary>
        /// <param name="faisceau">L'hypothèse à prolonger.</param>
        /// <param name="etat">L'état caché de l'encodeur.</param>
        /// <param name="masqueEncodeur">Le masque d'attention de la source.</param>
        /// <param name="cacheEncodeur">
        /// Le cache d'attention croisée, rempli au premier tour et constant ensuite.
        /// </param>
        /// <param name="premier">Vrai au tout premier tour, quand aucun cache n'existe.</param>
        /// <param name="cacheSuivant">Le tableau à remplir avec le cache produit.</param>
        /// <returns>Les logarithmes de vraisemblance de chaque jeton du vocabulaire.</returns>
        protected abstract float[] Avancer(
            FaisceauDeDecodage faisceau,
            DenseTensor<float> etat,
            DenseTensor<long> masqueEncodeur,
            DenseTensor<float>[] cacheEncodeur,
            bool premier,
            DenseTensor<float>[] cacheSuivant);

        /// <summary>
        /// Libère ce que la classe fille a ouvert.
        /// </summary>
        protected abstract void Liberer();

        /// <summary>
        /// Jeton imposé au tour indiqué, ou <c>null</c> si le modèle est libre.
        /// </summary>
        /// <remarks>
        /// Un modèle multilingue force la langue cible comme premier jeton produit :
        /// sans ça il traduirait vers n'importe laquelle des langues qu'il connaît.
        /// </remarks>
        /// <param name="tour">Le rang du jeton sur le point d'être produit.</param>
        /// <returns>L'identifiant imposé, ou <c>null</c>.</returns>
        protected virtual int? JetonImpose(int tour)
        {
            return null;
        }

        /// <summary>
        /// Identifiant par lequel commence le décodage.
        /// </summary>
        protected int DebutDuDecodage
        {
            get { return debutDuDecodage; }
        }

        /// <summary>
        /// Découpe le texte d'une zone en morceaux traduits séparément.
        /// </summary>
        /// <remarks>
        /// Par défaut le texte part d'un seul tenant. Un moteur entraîné strictement
        /// sur des phrases isolées redéfinit ceci pour découper : sans quoi il traduit
        /// la première phrase, produit sa marque de fin, et laisse tomber le reste.
        /// </remarks>
        /// <param name="texte">Le texte d'origine de la zone.</param>
        /// <returns>Les morceaux à traduire, dans l'ordre.</returns>
        protected virtual IReadOnlyList<string> Decouper(string texte)
        {
            return new[] { texte };
        }

        private string? Traduire(string texte)
        {
            List<string> traduites = new List<string>();

            foreach (string morceau in Decouper(texte))
            {
                string? traduit = TraduireUnMorceau(morceau);

                if (!string.IsNullOrWhiteSpace(traduit))
                {
                    traduites.Add(traduit);
                }
            }

            // Recollés par une espace : le texte français d'une bulle est une suite de
            // phrases, pas une liste.
            return traduites.Count == 0 ? null : string.Join(" ", traduites);
        }

        private string? TraduireUnMorceau(string texte)
        {
            long[] entree = VersIdentifiants(texte);

            if (entree.Length <= 1)
            {
                return null;
            }

            List<int> produits = Decoder(Encoder(entree), entree.Length);

            return produits.Count == 0 ? null : VersTexte(produits);
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
                    foreach ((int Jeton, double LogProb) suite in Suites(vraisemblances, tour))
                    {
                        candidats.Add(new FaisceauDeDecodage(
                            new List<int>(faisceau.Jetons) { suite.Jeton },
                            faisceau.Score + suite.LogProb,
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

        private List<(int Jeton, double LogProb)> Suites(float[] vraisemblances, int tour)
        {
            int? impose = JetonImpose(tour);

            return impose.HasValue
                ? new List<(int, double)> { (impose.Value, vraisemblances[impose.Value]) }
                : MeilleursJetons(vraisemblances);
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

        /// <summary>
        /// Convertit les scores bruts du modèle en logarithmes de probabilité.
        /// </summary>
        /// <param name="logits">La sortie brute du décodeur.</param>
        /// <returns>Un logarithme de probabilité par jeton du vocabulaire.</returns>
        protected float[] LogVraisemblances(Tensor<float> logits)
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

            // Les scores bruts ne sont pas comparables d'un tour à l'autre : on les
            // ramène en logarithmes de probabilité, qui eux s'additionnent le long
            // d'une hypothèse. Le retrait du maximum évite que l'exponentielle ne
            // déborde.
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

            // Le remplissage sert d'amorce au décodage mais ne doit jamais être produit.
            valeurs[identifiantDeRemplissage] = float.NegativeInfinity;

            return valeurs;
        }

        private List<(int Jeton, double LogProb)> MeilleursJetons(float[] vraisemblances)
        {
            // Sélection partielle plutôt qu'un tri : on cherche quelques valeurs parmi
            // des dizaines ou des centaines de milliers, à chaque tour et pour chaque
            // faisceau.
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

        /// <summary>
        /// Construit des tenseurs de cache de longueur nulle.
        /// </summary>
        /// <param name="nombre">Nombre de tenseurs à produire.</param>
        /// <returns>Un cache vide, accepté par le modèle au premier tour.</returns>
        protected DenseTensor<float>[] CacheVide(int nombre)
        {
            DenseTensor<float>[] cache = new DenseTensor<float>[nombre];

            for (int rang = 0; rang < nombre; rang++)
            {
                cache[rang] = new DenseTensor<float>(new[] { 1, tetes, 0, dimensionDeTete });
            }

            return cache;
        }

        /// <summary>
        /// Emballe un identifiant en tenseur d'entrée du décodeur.
        /// </summary>
        /// <param name="identifiant">Le jeton à donner au décodeur.</param>
        /// <returns>Un tenseur de forme [1, 1].</returns>
        protected static DenseTensor<long> UnJeton(int identifiant)
        {
            DenseTensor<long> jeton = new DenseTensor<long>(new[] { 1, 1 });

            jeton[0, 0] = identifiant;

            return jeton;
        }

        /// <summary>
        /// Emballe un booléen en tenseur d'entrée.
        /// </summary>
        /// <param name="valeur">La valeur à transmettre.</param>
        /// <returns>Un tenseur de forme [1].</returns>
        protected static DenseTensor<bool> Drapeau(bool valeur)
        {
            DenseTensor<bool> drapeau = new DenseTensor<bool>(new[] { 1 });

            drapeau[0] = valeur;

            return drapeau;
        }

        /// <summary>
        /// Recopie un tenseur rendu par ONNX Runtime.
        /// </summary>
        /// <remarks>
        /// Ces tenseurs pointent vers de la mémoire native libérée à la fin du tour :
        /// il faut les recopier pour les réutiliser au tour suivant.
        /// </remarks>
        /// <param name="source">Le tenseur à recopier.</param>
        /// <returns>Une copie indépendante.</returns>
        protected static DenseTensor<float> Copier(Tensor<float> source)
        {
            DenseTensor<float> dense = source.ToDenseTensor();

            return new DenseTensor<float>(dense.Buffer.ToArray(), dense.Dimensions.ToArray());
        }

        /// <summary>
        /// Vérifie qu'un fichier de l'export est présent et rend son chemin.
        /// </summary>
        /// <param name="dossier">Le dossier de l'export.</param>
        /// <param name="fichier">Le nom du fichier attendu.</param>
        /// <returns>Le chemin complet du fichier.</returns>
        /// <exception cref="FileNotFoundException">Levée si le fichier manque.</exception>
        protected static string Exiger(string dossier, string fichier)
        {
            string chemin = Path.Combine(dossier, fichier);

            if (!File.Exists(chemin))
            {
                throw new FileNotFoundException(
                    $"Le fichier {fichier} manque dans l'export du modèle.", chemin);
            }

            return chemin;
        }

        private void LireLaConfiguration(string chemin)
        {
            using JsonDocument configuration = JsonDocument.Parse(File.ReadAllText(chemin));
            JsonElement racine = configuration.RootElement;

            identifiantDeRemplissage = Entier(racine, "pad_token_id", 1);
            identifiantDeFin = Entier(racine, "eos_token_id", 2);
            debutDuDecodage = Entier(racine, "decoder_start_token_id", identifiantDeRemplissage);
            couches = Entier(racine, "decoder_layers", 6);
            tetes = Entier(racine, "decoder_attention_heads", 8);

            dimensionDeTete = Entier(racine, "d_model", 512) / tetes;
        }

        private static int Entier(JsonElement racine, string nom, int defaut)
        {
            return racine.TryGetProperty(nom, out JsonElement valeur)
                && valeur.ValueKind == JsonValueKind.Number
                ? valeur.GetInt32()
                : defaut;
        }

        #endregion
    }
}
