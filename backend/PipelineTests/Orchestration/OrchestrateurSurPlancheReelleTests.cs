using System.Runtime.Versioning;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Cadrage;
using ScanTrad.Pipeline.Effacement;
using ScanTrad.Pipeline.Lecture;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.OrdreDeLecture;
using ScanTrad.Pipeline.Orchestration;
using ScanTrad.Pipeline.Reecriture;
using ScanTrad.Pipeline.Traduction;

namespace ScanTrad.PipelineTests.Orchestration
{
    /// <summary>
    /// Le grand test : traite une vraie planche brute de bout en bout par le point
    /// d'entrée du pipeline, et attache l'image composée pour qu'un humain aille la
    /// regarder.
    /// </summary>
    /// <remarks>
    /// Contrairement aux autres tests d'intégration, celui-ci ne part pas de
    /// <see cref="LectureDeLaPlancheDEssai"/> : il construit son propre lecteur et
    /// relit la planche depuis zéro, parce que la lecture est justement l'une des six
    /// étapes que l'orchestrateur doit enchaîner — la sauter reviendrait à ne pas
    /// tester le point d'entrée en entier.
    /// <para>
    /// Les six étapes sont les vraies implémentations, assemblées à la main comme le
    /// ferait un jour l'API : c'est ce test qui prouve que l'assemblage complet tient
    /// debout, pas seulement chaque étape prise séparément.
    /// </para>
    /// <para>
    /// Sans les modèles de traduction locaux, ce test échoue avec un
    /// <see cref="System.IO.FileNotFoundException"/> explicite — voir le README.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    [SupportedOSPlatform("windows")]
    public class OrchestrateurSurPlancheReelleTests
    {
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec le collecteur de sortie.
        /// </summary>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public OrchestrateurSurPlancheReelleTests(ITestOutputHelper sortie)
        {
            this.sortie = sortie;
        }

        /// <summary>
        /// Traite la planche d'essai brute de bout en bout et attache l'image
        /// composée.
        /// </summary>
        /// <param name="moteur">Le moteur de traduction mis à l'épreuve.</param>
        [Theory]
        [InlineData(MoteurDeTraduction.OpusMt)]
        [InlineData(MoteurDeTraduction.Nllb)]
        public async Task TraiterAsync_SurUnePlancheBrute_DonneLImageComposeeAVoir(MoteurDeTraduction moteur)
        {
            // Cette édition est retournée : elle se lit de gauche à droite.
            Planche brute = new Planche(await PlancheDEssai.ChargerAsync(), SensDeLecture.GaucheADroite);

            using ILecteurDePlanche lecteur = new LecteurDePlanche(PlancheDEssai.TrouverLeModele());
            IAjusteurDeRectangle ajusteur = new AjusteurDeRectangleParRasterisation();
            IOrdonnanceurDeZones ordonnanceur = new OrdonnanceurDeZones();
            using ITraducteur traducteur = new ChoixDuTraducteur().Creer(moteur);
            IEffaceurDeTexte effaceur = new EffaceurParRemplissage();
            IReecrivainDeTexte reecrivain = new ReecrivainParAjustement();

            IOrchestrateurDePipeline orchestrateur =
                new Orchestrateur(lecteur, ajusteur, ordonnanceur, traducteur, effaceur, reecrivain);

            Planche composee = await orchestrateur.TraiterAsync(brute, TestContext.Current.CancellationToken);

            int avecTraduction = composee.Zones
                .Count(zone => zone.Bulle != null && !string.IsNullOrEmpty(zone.TexteTraduit));

            Assert.NotEmpty(composee.Zones);
            Assert.NotEqual(brute.Image, composee.Image);
            Assert.True(avecTraduction > 0, "Aucune zone traduite : le test ne prouverait rien.");

            Assert.Equal(
                Enumerable.Range(0, composee.Zones.Count),
                composee.Zones.Select(zone => zone.OrdreDeLecture!.Value).OrderBy(rang => rang));

            TestContext.Current.AddAttachment($"apercu-orchestrateur-{moteur}", composee.Image, "image/png");

            string fichier = Deposer(composee, $"Akashic-orchestrateur-{moteur}.png");

            sortie.WriteLine($"Moteur            : {moteur}");
            sortie.WriteLine($"Blocs             : {composee.Zones.Count}");
            sortie.WriteLine($"Bulles réécrites  : {avecTraduction}");
            sortie.WriteLine($"Poids composé     : {composee.Image.Length / 1024} Ko");
            sortie.WriteLine(string.Empty);
            sortie.WriteLine("Image composée jointe au test, et déposée ici :");
            sortie.WriteLine(fichier);
        }

        private static string Deposer(Planche planche, string nom)
        {
            string dossier = Path.Combine(AppContext.BaseDirectory, "sorties");

            Directory.CreateDirectory(dossier);

            string fichier = Path.Combine(dossier, nom);

            File.WriteAllBytes(fichier, planche.Image);

            return fichier;
        }
    }
}
