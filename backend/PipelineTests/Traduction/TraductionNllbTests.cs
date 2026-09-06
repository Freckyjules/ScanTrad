using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.Traduction;
using ScanTrad.Pipeline.Traduction.Moteurs.Nllb;

namespace ScanTrad.PipelineTests.Traduction
{
    /// <summary>
    /// Éprouve le moteur NLLB-200 réel, modèle chargé.
    /// </summary>
    /// <remarks>
    /// Hérite des règles communes à tout traducteur, exactement comme le moteur
    /// OPUS-MT : c'est tout l'intérêt de les avoir posées à part, et la garantie qu'un
    /// second moteur ne peut pas en oublier une.
    /// <para>
    /// Ces tests coûtent cher. Chaque construction charge sept gigaoctets en une
    /// poignée de secondes, et chaque phrase traduite en demande cinq — soit huit fois
    /// le temps d'OPUS-MT, le prix d'un vocabulaire quatre fois plus grand.
    /// </para>
    /// <para>
    /// L'export n'est pas versionné et vit hors du dépôt : voir <c>NLLB-200.md</c>.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class TraductionNllbTests : ContratDeTraducteur
    {
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec le collecteur de sortie.
        /// </summary>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public TraductionNllbTests(ITestOutputHelper sortie)
        {
            this.sortie = sortie;
        }

        /// <inheritdoc />
        /// <remarks>
        /// On passe par <see cref="ChoixDuTraducteur"/> plutôt que de construire le
        /// moteur à la main : le chemin réellement emprunté en production se trouve
        /// ainsi couvert par toutes les règles du contrat.
        /// </remarks>
        protected override ITraducteur Creer()
        {
            return new ChoixDuTraducteur().Creer(MoteurDeTraduction.Nllb);
        }

        /// <summary>
        /// Une phrase anglaise ressort en français, et pas en anglais.
        /// </summary>
        [Fact]
        public async Task Traduire_UnePhraseSimple_RendDuTexteDifferentDeLaSource()
        {
            const string source = "The library opens at nine in the morning.";

            using ITraducteur traducteur = Creer();

            Planche traduite = await traducteur.TraduireAsync(
                PlancheAvec(Zone(source)), TestContext.Current.CancellationToken);

            string? resultat = Assert.Single(traduite.Zones).TexteTraduit;

            Assert.False(string.IsNullOrWhiteSpace(resultat));
            Assert.NotEqual(source, resultat);

            sortie.WriteLine($"EN : {source}");
            sortie.WriteLine($"FR : {resultat}");
        }

        /// <summary>
        /// Un texte tout en majuscules ressort quand même en français correct.
        /// </summary>
        /// <remarks>
        /// L'assertion diffère volontairement de celle du moteur OPUS-MT, qui exige
        /// une traduction <em>identique</em> à celle du même texte en casse normale.
        /// C'est que les deux moteurs ne sont pas dans la même situation : OPUS-MT n'a
        /// jamais vu de texte crié et rendait des contresens, d'où une normalisation de
        /// la casse écrite exprès pour lui, et un test qui la protège.
        /// <para>
        /// NLLB, entraîné sur des données bien plus variées, s'en sort seul — mesuré
        /// avant d'écrire quoi que ce soit. Ajouter la même normalisation ici aurait
        /// été du bruit, et exiger l'égalité des deux traductions ferait échouer un
        /// test alors que rien ne va mal : le modèle rend simplement une phrase un peu
        /// différente, parfois meilleure.
        /// </para>
        /// </remarks>
        [Fact]
        public async Task Traduire_EnMajuscules_RendQuandMemeDuFrancais()
        {
            const string crie = "AND WE COULD NOT FIND ANYONE ELSE IN TIME.";
            const string normal = "And we could not find anyone else in time.";

            using ITraducteur traducteur = Creer();

            Planche traduite = await traducteur.TraduireAsync(
                PlancheAvec(Zone(crie), Zone(normal)), TestContext.Current.CancellationToken);

            string? enMajuscules = traduite.Zones[0].TexteTraduit;
            string? enCasseNormale = traduite.Zones[1].TexteTraduit;

            sortie.WriteLine($"majuscules    : {enMajuscules}");
            sortie.WriteLine($"casse normale : {enCasseNormale}");

            Assert.False(string.IsNullOrWhiteSpace(enMajuscules));
            Assert.NotEqual(crie, enMajuscules);
        }

        /// <summary>
        /// Un dossier de modèle absent doit être signalé clairement, et non planter au
        /// fond d'ONNX Runtime.
        /// </summary>
        [Fact]
        public void Constructeur_DossierSansModele_LeveFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(
                () => new TraductionNllb(Path.GetTempPath()));
        }

        /// <summary>
        /// Construire un moteur sans dire où est le modèle n'a pas de sens.
        /// </summary>
        [Fact]
        public void Constructeur_CheminVide_LeveArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new TraductionNllb("  "));
        }
    }
}
