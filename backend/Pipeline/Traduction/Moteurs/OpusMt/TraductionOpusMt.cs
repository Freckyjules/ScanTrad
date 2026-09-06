using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System.Text.Json;

namespace ScanTrad.Pipeline.Traduction.Moteurs.OpusMt
{
    /// <summary>
    /// Traduit avec OPUS-MT exporté en ONNX, entièrement en local.
    /// </summary>
    /// <remarks>
    /// Modèle dédié à une seule paire de langues, donc petit et rapide : il tourne sur
    /// processeur en quelques centaines de millisecondes par bulle.
    /// <para>
    /// Le piège de la tokenisation, mesuré sur les fichiers réels : <c>source.spm</c>
    /// porte 32 000 pièces avec ses propres identifiants, alors que le modèle en
    /// attend 59 514 numérotés autrement — aucune correspondance entre les deux. On
    /// tokenise donc en <em>pièces</em> avec SentencePiece, puis on traduit ces pièces
    /// en identifiants par <c>vocab.json</c>. Utiliser directement les identifiants de
    /// SentencePiece compile, s'exécute, et rend du charabia.
    /// </para>
    /// <para>
    /// Son export ne porte qu'un décodeur, piloté par un drapeau qui dit s'il faut
    /// emprunter la branche avec cache. Voir <c>OPUS-MT.md</c> pour le régénérer.
    /// </para>
    /// </remarks>
    public partial class TraductionOpusMt : TraductionSeq2SeqOnnx
    {
        #region Constantes

        private const string FichierEncodeur = "encoder_model.onnx";
        private const string FichierDecodeur = "decoder_model_merged.onnx";
        private const string FichierTokeniseur = "source.spm";
        private const string FichierVocabulaire = "vocab.json";

        #endregion

        #region Attributs

        private InferenceSession encodeur;
        private InferenceSession decodeur;
        private SentencePieceTokenizer tokeniseur;
        private Dictionary<string, int> versIdentifiant;
        private string[] versPiece;
        private int identifiantInconnu;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Charge le modèle exporté depuis le dossier indiqué.
        /// </summary>
        /// <param name="cheminDuDossier">
        /// Dossier contenant l'export ONNX : le modèle, le tokeniseur, le vocabulaire
        /// et la configuration.
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
        /// <param name="cheminDuDossier">Dossier contenant l'export ONNX.</param>
        /// <param name="nombreDeFaisceaux">
        /// Nombre d'hypothèses menées de front. Un seul revient à un décodage glouton,
        /// plus rapide mais moins bon.
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
            : base(cheminDuDossier, nombreDeFaisceaux)
        {
            versIdentifiant = new Dictionary<string, int>();
            versPiece = Array.Empty<string>();

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

        #region Méthodes privées

        /// <inheritdoc />
        protected override void Liberer()
        {
            encodeur.Dispose();
            decodeur.Dispose();
        }

        /// <inheritdoc />
        protected override long[] VersIdentifiants(string texte)
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

            identifiants[pieces.Count] = IdentifiantDeFin;

            return identifiants;
        }

        /// <inheritdoc />
        protected override string VersTexte(List<int> produits)
        {
            string assemble = string.Concat(produits
                .Where(identifiant => identifiant >= 0 && identifiant < versPiece.Length)
                .Select(identifiant => versPiece[identifiant]));

            return assemble.Replace(MarqueDeMot, ' ').Trim();
        }

        /// <inheritdoc />
        protected override DenseTensor<float> Encoder(long[] entree)
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

            return Copier(sortie.First().AsTensor<float>());
        }

        /// <inheritdoc />
        protected override float[] Avancer(
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

            for (int couche = 0; couche < Couches; couche++)
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

            for (int couche = 0; couche < Couches; couche++)
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

        #endregion
    }
}
