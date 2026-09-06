using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ScanTrad.Pipeline.Traduction.Moteurs.Nllb
{
    /// <summary>
    /// Traduit avec NLLB-200 exporté en ONNX, entièrement en local.
    /// </summary>
    /// <remarks>
    /// Modèle multilingue de Meta couvrant deux cents langues. Là où OPUS-MT est dédié
    /// à une seule paire, celui-ci les porte toutes dans un même réseau — d'où sa
    /// taille et sa lenteur : sept gigaoctets à charger, un vocabulaire de 256 206
    /// jetons à parcourir à chaque tour contre 59 514.
    /// <para>
    /// Trois choses le distinguent d'OPUS-MT, toutes mesurées sur les fichiers réels.
    /// </para>
    /// <para>
    /// <b>Les identifiants.</b> Ceux du modèle valent exactement ceux de SentencePiece
    /// plus un — vérifié sur 19 000 pièces, écart unique. C'est l'héritage de fairseq,
    /// qui range ses quatre jetons spéciaux en tête et décale tout le reste.
    /// </para>
    /// <para>
    /// <b>Les langues.</b> La langue source s'annonce en tête de l'entrée, et la langue
    /// cible doit être <em>imposée</em> comme premier jeton produit. Sans ça le modèle
    /// traduirait vers n'importe laquelle des deux cents langues qu'il connaît.
    /// </para>
    /// <para>
    /// <b>Deux décodeurs séparés</b>, et non un seul piloté par un drapeau : le
    /// décodeur fusionné dépasserait la limite de deux gigaoctets du format, et son
    /// export échoue. On emploie donc le premier au tour initial et le second ensuite.
    /// Le second ne reçoit pas l'état de l'encodeur — il le retrouve dans le cache
    /// d'attention croisée — et ne rend que le cache du décodeur.
    /// </para>
    /// </remarks>
    public class TraductionNllb : TraductionSeq2SeqOnnx
    {
        #region Constantes

        private const string FichierEncodeur = "encoder_model.onnx";
        private const string FichierDecodeur = "decoder_model.onnx";
        private const string FichierDecodeurAvecCache = "decoder_with_past_model.onnx";
        private const string FichierTokeniseur = "sentencepiece.bpe.model";
        private const string FichierDesJetonsAjoutes = "tokenizer.json";

        /// <summary>
        /// Écart entre les identifiants de SentencePiece et ceux du modèle.
        /// </summary>
        /// <remarks>
        /// Mesuré sur 19 000 pièces : l'écart vaut un, sans une seule exception. Les
        /// employer tels quels rendrait un texte décalé d'un jeton, donc du charabia
        /// que rien ne signalerait.
        /// </remarks>
        private const int DecalageDesIdentifiants = 1;

        /// <summary>
        /// Code de la langue source, tel que le modèle le nomme.
        /// </summary>
        private const string LangueSource = "eng_Latn";

        /// <summary>
        /// Code de la langue cible.
        /// </summary>
        private const string LangueCible = "fra_Latn";

        #endregion

        #region Attributs

        private InferenceSession encodeur;
        private InferenceSession decodeur;
        private InferenceSession decodeurAvecCache;
        private SentencePieceTokenizer tokeniseur;
        private int jetonDeLaSource;
        private int jetonDeLaCible;
        private int identifiantInconnu;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Charge le modèle exporté depuis le dossier indiqué.
        /// </summary>
        /// <param name="cheminDuDossier">
        /// Dossier contenant l'export ONNX : l'encodeur, les deux décodeurs, le
        /// tokeniseur et la configuration.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="cheminDuDossier"/> est vide.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Levée si l'un des fichiers de l'export manque.
        /// </exception>
        public TraductionNllb(string cheminDuDossier)
            : this(cheminDuDossier, NombreDeFaisceauxParDefaut)
        {
        }

        /// <summary>
        /// Charge le modèle et fixe la largeur de la recherche en faisceau.
        /// </summary>
        /// <param name="cheminDuDossier">Dossier contenant l'export ONNX.</param>
        /// <param name="nombreDeFaisceaux">
        /// Nombre d'hypothèses menées de front. Chacune coûte cher sur ce modèle : son
        /// vocabulaire est quatre fois plus grand que celui d'OPUS-MT.
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
        public TraductionNllb(string cheminDuDossier, int nombreDeFaisceaux)
            : base(cheminDuDossier, nombreDeFaisceaux)
        {
            using FileStream modele = File.OpenRead(Exiger(cheminDuDossier, FichierTokeniseur));

            tokeniseur = SentencePieceTokenizer.Create(
                modele, addBeginningOfSentence: false, addEndOfSentence: false);

            identifiantInconnu = tokeniseur.UnknownId + DecalageDesIdentifiants;

            // Les codes de langue ne sont pas dans le modèle SentencePiece : ils
            // s'ajoutent après lui, et leur rang se lit dans le fichier du tokeniseur.
            // On l'ouvre une seule fois, il pèse une trentaine de mégaoctets.
            Dictionary<string, int> langues =
                LireLesCodesDeLangue(cheminDuDossier, LangueSource, LangueCible);

            jetonDeLaSource = langues[LangueSource];
            jetonDeLaCible = langues[LangueCible];

            encodeur = new InferenceSession(Exiger(cheminDuDossier, FichierEncodeur));
            decodeur = new InferenceSession(Exiger(cheminDuDossier, FichierDecodeur));
            decodeurAvecCache =
                new InferenceSession(Exiger(cheminDuDossier, FichierDecodeurAvecCache));
        }

        #endregion

        #region Méthodes privées

        /// <inheritdoc />
        protected override void Liberer()
        {
            encodeur.Dispose();
            decodeur.Dispose();
            decodeurAvecCache.Dispose();
        }

        /// <inheritdoc />
        protected override long[] VersIdentifiants(string texte)
        {
            IReadOnlyList<EncodedToken> pieces = tokeniseur.EncodeToTokens(texte, out string? _);

            // La langue source en tête, la marque de fin en queue : c'est la forme
            // qu'attend le modèle.
            long[] identifiants = new long[pieces.Count + 2];

            identifiants[0] = jetonDeLaSource;

            for (int rang = 0; rang < pieces.Count; rang++)
            {
                identifiants[rang + 1] = Traduire(pieces[rang].Id);
            }

            identifiants[pieces.Count + 1] = IdentifiantDeFin;

            return identifiants;
        }

        /// <inheritdoc />
        protected override string VersTexte(List<int> produits)
        {
            // On repasse dans l'autre sens avant de rendre les identifiants à
            // SentencePiece, et on écarte le code de langue, qui n'est pas du texte.
            IEnumerable<int> identifiants = produits
                .Where(identifiant => identifiant != jetonDeLaCible
                    && identifiant >= DecalageDesIdentifiants)
                .Select(identifiant => identifiant - DecalageDesIdentifiants);

            return tokeniseur.Decode(identifiants).Trim();
        }

        /// <inheritdoc />
        /// <remarks>
        /// NLLB est entraîné sur des paires d'<em>une</em> phrase, son corpus ayant été
        /// bâti par alignement automatique avec un filtrage strict. Donnée deux phrases,
        /// il traduit la première, produit sa marque de fin, et laisse tomber la
        /// seconde. Observé sur la planche d'essai : une bulle assemblée en deux
        /// phrases ressortait amputée de moitié.
        /// <para>
        /// OPUS-MT s'en sort mieux — son corpus contient beaucoup de sous-titres, où un
        /// segment porte volontiers plusieurs phrases. C'est une tolérance et non une
        /// garantie : mesuré sur la même planche, il tronque lui aussi un bloc sur
        /// sept. Le découpage lui profiterait.
        /// </para>
        /// </remarks>
        protected override IReadOnlyList<string> Decouper(string texte)
        {
            return DecoupageEnPhrases.Decouper(texte);
        }

        /// <inheritdoc />
        protected override int? JetonImpose(int tour)
        {
            // Le tout premier jeton produit désigne la langue de sortie. Le laisser
            // libre reviendrait à laisser le modèle choisir parmi deux cents langues.
            return tour == 0 ? jetonDeLaCible : null;
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
                NamedOnnxValue.CreateFromTensor("input_ids", UnJeton(faisceau.Dernier))
            };

            if (premier)
            {
                // Le décodeur initial part de l'état de l'encodeur et ne connaît aucun
                // cache.
                entrees.Add(NamedOnnxValue.CreateFromTensor("encoder_hidden_states", etat));
            }
            else
            {
                // Celui à cache ne reçoit pas l'état : il le retrouve dans le cache
                // d'attention croisée qu'on lui repasse.
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
            }

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> sortie =
                (premier ? decodeur : decodeurAvecCache).Run(entrees);

            Dictionary<string, DisposableNamedOnnxValue> parNom =
                sortie.ToDictionary(valeur => valeur.Name);

            for (int couche = 0; couche < Couches; couche++)
            {
                cacheSuivant[couche * 2] =
                    Copier(parNom[$"present.{couche}.decoder.key"].AsTensor<float>());
                cacheSuivant[(couche * 2) + 1] =
                    Copier(parNom[$"present.{couche}.decoder.value"].AsTensor<float>());

                // Seul le décodeur initial rend le cache d'attention croisée ; il ne
                // dépend que de la source et ne bouge plus ensuite.
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

        private long Traduire(int identifiantDeSentencePiece)
        {
            return identifiantDeSentencePiece == tokeniseur.UnknownId
                ? identifiantInconnu
                : identifiantDeSentencePiece + DecalageDesIdentifiants;
        }

        private static Dictionary<string, int> LireLesCodesDeLangue(string dossier, params string[] codes)
        {
            using JsonDocument fichier =
                JsonDocument.Parse(File.ReadAllText(Exiger(dossier, FichierDesJetonsAjoutes)));

            Dictionary<string, int> trouves = new Dictionary<string, int>();

            if (fichier.RootElement.TryGetProperty("added_tokens", out JsonElement ajoutes))
            {
                foreach (JsonElement jeton in ajoutes.EnumerateArray())
                {
                    if (jeton.TryGetProperty("content", out JsonElement contenu)
                        && contenu.GetString() is string nom
                        && codes.Contains(nom)
                        && jeton.TryGetProperty("id", out JsonElement identifiant))
                    {
                        trouves[nom] = identifiant.GetInt32();
                    }
                }
            }

            foreach (string code in codes)
            {
                if (!trouves.ContainsKey(code))
                {
                    throw new InvalidOperationException(
                        $"Le code de langue {code} est introuvable dans le tokeniseur de l'export.");
                }
            }

            return trouves;
        }

        #endregion
    }
}
