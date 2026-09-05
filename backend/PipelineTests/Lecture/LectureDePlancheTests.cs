using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Lecture;
using ScanTrad.Pipeline.Models;
using Xunit.Abstractions;

namespace ScanTrad.PipelineTests.Lecture
{
    /// <summary>
    /// Fait tourner les vrais lecteurs sur une vraie planche.
    /// </summary>
    /// <remarks>
    /// Le contrat de <see cref="ILecteurDePage"/> est vérifié une seule fois, rejoué
    /// sur chaque implémentation : une nouvelle n'a qu'à s'ajouter à
    /// <see cref="LesLecteurs"/> pour être contrôlée comme les autres.
    /// <para>
    /// Ces tests sont lents — ils chargent des modèles et lisent une image entière —
    /// et s'écartent des exécutions courantes avec
    /// <c>dotnet test --filter "Categorie!=Integration"</c>. Le lecteur
    /// comic-text-detector demande en plus le fichier
    /// <c>backend/modeles/comictextdetector.onnx</c>, qui n'est pas versionné.
    /// </para>
    /// <para>
    /// Rien ici ne vérifie <em>ce que</em> les moteurs ont lu : figer le résultat d'un
    /// modèle qu'on ne maîtrise pas ferait casser le test à chaque montée de version.
    /// On vérifie que la sortie est exploitable par la suite du pipeline.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class LectureDePlancheTests
    {
        private const string LecteurPaddleOcr = "PaddleOCR";
        private const string LecteurComicTextDetector = "ComicTextDetector";

        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec le collecteur de sortie fourni par xUnit.
        /// </summary>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public LectureDePlancheTests(ITestOutputHelper sortie)
        {
            this.sortie = sortie;
        }

        /// <summary>
        /// Les implémentations à confronter au contrat commun.
        /// </summary>
        public static TheoryData<string> LesLecteurs
        {
            get
            {
                return new TheoryData<string>
                {
                    LecteurPaddleOcr,
                    LecteurComicTextDetector
                };
            }
        }

        /// <summary>
        /// Quel que soit le moteur, une lecture rend des zones exploitables : une
        /// géométrie non dégénérée, un texte non vide, une confiance dans les bornes —
        /// et rien qui relève des étapes suivantes.
        /// </summary>
        /// <param name="nomDuLecteur">L'implémentation à mettre à l'épreuve.</param>
        [Theory]
        [MemberData(nameof(LesLecteurs))]
        public async Task LireAsync_SurUnePlancheReelle_RespecteLeContrat(string nomDuLecteur)
        {
            byte[] image = await PlancheDEssai.ChargerAsync();

            using ILecteurDePage lecteur = ConstruireLeLecteur(nomDuLecteur);

            IReadOnlyList<ZoneDeTexte> zones = await lecteur.LireAsync(image);

            Assert.NotNull(zones);
            Assert.NotEmpty(zones);

            foreach (ZoneDeTexte zone in zones)
            {
                VerifierQueLaZoneEstExploitable(zone);
            }

            Decrire(nomDuLecteur, zones);
        }

        /// <summary>
        /// Ce que comic-text-detector apporte et que PaddleOCR seul ne sait pas
        /// faire : retrouver le contour des bulles. N'a donc pas sa place dans le
        /// contrat commun.
        /// </summary>
        [Fact]
        public async Task LireAsync_AvecComicTextDetector_RetrouveDesBulles()
        {
            byte[] image = await PlancheDEssai.ChargerAsync();

            using LecteurDePageComicTextDetector lecteur =
                new LecteurDePageComicTextDetector(PlancheDEssai.TrouverLeModele());

            IReadOnlyList<ZoneDeTexte> zones = await ((ILecteurDePage)lecteur).LireAsync(image);

            int avecBulle = zones.Count(zone => zone.Bulle != null);

            Assert.True(avecBulle > 0, "Aucune bulle retrouvée sur une planche qui en est pleine.");

            foreach (ZoneDeTexte zone in zones.Where(zone => zone.Bulle != null))
            {
                Assert.True(zone.Bulle!.Contour.Count >= 3);
            }

            sortie.WriteLine($"{avecBulle} bulles retrouvées sur {zones.Count} zones.");
            sortie.WriteLine(string.Empty);

            foreach (ZoneDeTexte zone in zones)
            {
                string bulle = zone.Bulle == null
                    ? "sans bulle"
                    : $"{zone.Bulle.Contour.Count} pts centrés en {zone.Bulle.Centre}";

                sortie.WriteLine($"[{zone.Confiance:0.00}] {zone.TexteOriginal,-32} | {bulle}");
            }
        }

        private static ILecteurDePage ConstruireLeLecteur(string nom)
        {
            switch (nom)
            {
                case LecteurPaddleOcr:
                    return new LecteurDePagePaddleOcr();

                case LecteurComicTextDetector:
                    return new LecteurDePageComicTextDetector(PlancheDEssai.TrouverLeModele());

                default:
                    throw new ArgumentException($"Lecteur inconnu : {nom}", nameof(nom));
            }
        }

        private static void VerifierQueLaZoneEstExploitable(ZoneDeTexte zone)
        {
            // Ce que l'étape de lecture doit avoir rempli.
            Assert.NotNull(zone.Quadrilatere);
            Assert.False(string.IsNullOrWhiteSpace(zone.TexteOriginal));
            Assert.InRange(zone.Confiance, 0, 1);

            // Une géométrie plate ne serait exploitable ni pour effacer ni pour réécrire.
            Assert.True(zone.Quadrilatere.Largeur > 0, "La zone a une largeur nulle.");
            Assert.True(zone.Quadrilatere.Hauteur > 0, "La zone a une hauteur nulle.");

            // Ce que l'étape de lecture ne doit surtout pas avoir rempli : sinon une
            // responsabilité a glissé dans le lecteur.
            Assert.Null(zone.TexteTraduit);
            Assert.Null(zone.OrdreDeLecture);

            // La bulle a le droit d'être absente : tous les moteurs ne savent pas la
            // détecter, et un texte hors bulle n'en a pas.
            if (zone.Bulle != null)
            {
                Assert.True(zone.Bulle.Contour.Count >= 3);
            }
        }

        private void Decrire(string nomDuLecteur, IReadOnlyList<ZoneDeTexte> zones)
        {
            sortie.WriteLine($"Lecteur       : {nomDuLecteur}");
            sortie.WriteLine($"Planche       : {PlancheDEssai.Nom}");
            sortie.WriteLine($"Zones lues    : {zones.Count}");
            sortie.WriteLine($"Avec bulle    : {zones.Count(zone => zone.Bulle != null)} sur {zones.Count}");
            sortie.WriteLine($"Confiance moy.: {zones.Average(zone => zone.Confiance):0.###}");
            sortie.WriteLine(string.Empty);

            foreach (ZoneDeTexte zone in zones)
            {
                sortie.WriteLine($"[{zone.Confiance:0.00}] {zone.TexteOriginal}");
            }
        }
    }
}
