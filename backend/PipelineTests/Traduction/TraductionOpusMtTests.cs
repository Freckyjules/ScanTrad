using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.Traduction;
using ScanTrad.Pipeline.Traduction.OpusMt;

namespace ScanTrad.PipelineTests.Traduction
{
    /// <summary>
    /// Éprouve le moteur OPUS-MT réel, modèle chargé.
    /// </summary>
    /// <remarks>
    /// Hérite des règles communes à tout traducteur, qui sont ainsi vérifiées sur le
    /// vrai moteur et non seulement sur un factice — c'est tout l'intérêt d'avoir posé
    /// ces règles à part.
    /// <para>
    /// Rien ici ne fige une traduction précise : une montée de version du modèle
    /// changerait le français rendu sans rien casser. On vérifie qu'il rend du texte,
    /// et qu'il le rend de la même façon quelle que soit la casse d'entrée.
    /// </para>
    /// <para>
    /// L'export n'est pas versionné : ces tests demandent le modèle décrit dans
    /// <c>OPUS-MT.md</c>.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class TraductionOpusMtTests : ContratDeTraducteur
    {
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec le collecteur de sortie.
        /// </summary>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public TraductionOpusMtTests(ITestOutputHelper sortie)
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
            return new ChoixDuTraducteur(ModeleOpusMt.Trouver())
                .Creer(MoteurDeTraduction.OpusMt);
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
        /// Un texte tout en majuscules se traduit comme le même texte en casse
        /// normale.
        /// </summary>
        /// <remarks>
        /// C'est la régression la plus coûteuse que ce moteur puisse subir, et elle
        /// serait invisible sans ce test. Le modèle n'a jamais vu de texte crié :
        /// mesuré avant correction, trois phrases d'essai sur trois ressortaient
        /// fausses en majuscules — contresens et mots inventés — et justes en casse
        /// normale. Or l'OCR d'une planche ne produit que des majuscules.
        /// </remarks>
        [Fact]
        public async Task Traduire_EnMajuscules_DonneLeMemeResultatQuEnCasseNormale()
        {
            const string crie = "AND WE COULD NOT FIND ANYONE ELSE IN TIME.";
            const string normal = "And we could not find anyone else in time.";

            using ITraducteur traducteur = Creer();

            Planche traduite = await traducteur.TraduireAsync(
                PlancheAvec(Zone(crie), Zone(normal)), TestContext.Current.CancellationToken);

            string? enMajuscules = traduite.Zones[0].TexteTraduit;
            string? enCasseNormale = traduite.Zones[1].TexteTraduit;

            sortie.WriteLine($"majuscules : {enMajuscules}");
            sortie.WriteLine($"casse normale : {enCasseNormale}");

            Assert.False(string.IsNullOrWhiteSpace(enMajuscules));
            Assert.Equal(enCasseNormale, enMajuscules);
        }

        /// <summary>
        /// Un dossier de modèle absent doit être signalé clairement, et non planter au
        /// fond d'ONNX Runtime.
        /// </summary>
        [Fact]
        public void Constructeur_DossierSansModele_LeveFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(
                () => new TraductionOpusMt(Path.GetTempPath()));
        }

        /// <summary>
        /// Construire un moteur sans dire où est le modèle n'a pas de sens.
        /// </summary>
        [Fact]
        public void Constructeur_CheminVide_LeveArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new TraductionOpusMt("  "));
        }
    }
}
