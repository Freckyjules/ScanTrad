using OpenCvSharp;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Lecture
{
    /// <summary>
    /// Vérifie ce que la lecture d'une vraie planche produit.
    /// </summary>
    /// <remarks>
    /// La lecture se fait en trois temps : le modèle manga situe les blocs de
    /// dialogue, chaque bloc est découpé et lu à pleine résolution, et le contour de
    /// sa bulle est reconstruit par diffusion.
    /// <para>
    /// La planche n'est lue qu'une fois pour toute la classe, via
    /// <see cref="LectureDeLaPlancheDEssai"/> : charger le modèle et analyser l'image
    /// coûte une vingtaine de secondes.
    /// </para>
    /// <para>
    /// Rien ici ne vérifie <em>ce que</em> le moteur a lu : figer le résultat d'un
    /// modèle qu'on ne maîtrise pas ferait casser le test à chaque montée de version.
    /// On vérifie que la sortie est exploitable par la suite du pipeline.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class LectureDePlancheTests : IClassFixture<LectureDeLaPlancheDEssai>
    {
        private readonly LectureDeLaPlancheDEssai lecture;
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec la lecture partagée et le collecteur de sortie.
        /// </summary>
        /// <param name="lecture">La planche d'essai, déjà lue.</param>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public LectureDePlancheTests(LectureDeLaPlancheDEssai lecture, ITestOutputHelper sortie)
        {
            this.lecture = lecture;
            this.sortie = sortie;
        }

        /// <summary>
        /// La lecture rend des zones exploitables : une géométrie non dégénérée, un
        /// texte non vide, une confiance dans les bornes — et rien qui relève des
        /// étapes suivantes.
        /// </summary>
        [Fact]
        public void Lecture_RespecteLeContrat()
        {
            Assert.NotEmpty(lecture.Zones);

            foreach (ZoneDeTexte zone in lecture.Zones)
            {
                // Ce que la lecture doit avoir rempli.
                Assert.NotNull(zone.Rectangle);
                Assert.False(string.IsNullOrWhiteSpace(zone.TexteOriginal));
                Assert.InRange(zone.Confiance, 0, 1);

                // Une géométrie plate ne serait exploitable ni pour effacer ni pour
                // réécrire.
                Assert.True(zone.Rectangle.Largeur > 0, "La zone a une largeur nulle.");
                Assert.True(zone.Rectangle.Hauteur > 0, "La zone a une hauteur nulle.");

                // Ce que la lecture ne doit surtout pas avoir rempli : sinon une
                // responsabilité a glissé dans le lecteur.
                Assert.Null(zone.TexteTraduit);
                Assert.Null(zone.OrdreDeLecture);
            }

            Decrire();
        }

        /// <summary>
        /// Le modèle rend des blocs entiers et non des lignes.
        /// </summary>
        /// <remarks>
        /// Le nombre de mots par zone le dit mieux qu'un nombre de zones : une lecture
        /// ligne par ligne donne un ou deux mots par zone, une lecture par blocs en
        /// donne une phrase. Le critère ne dépend donc pas du nombre de bulles que
        /// porte la planche.
        /// </remarks>
        [Fact]
        public void Lecture_RendDesBlocsEtNonDesLignes()
        {
            double motsParZone = lecture.Zones.Average(
                zone => zone.TexteOriginal.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

            Assert.True(
                motsParZone >= 3,
                $"{motsParZone:0.0} mots par zone : la lecture semble être retombée sur des lignes.");
        }

        /// <summary>
        /// Le contour des bulles est reconstruit pour la plupart des blocs.
        /// </summary>
        /// <remarks>
        /// On exige une majorité plutôt qu'un chiffre exact : un compte figé casserait
        /// à la moindre montée de version du modèle, alors qu'un simple « au moins
        /// une » laisserait passer une régression qui n'en trouverait plus qu'une.
        /// </remarks>
        [Fact]
        public void Lecture_RetrouveLaPlupartDesBulles()
        {
            int avecBulle = lecture.Zones.Count(zone => zone.Bulle != null);

            Assert.True(
                avecBulle * 2 >= lecture.Zones.Count,
                $"{avecBulle} bulles sur {lecture.Zones.Count} blocs : trop peu.");

            foreach (ZoneDeTexte zone in lecture.Zones.Where(zone => zone.Bulle != null))
            {
                Assert.True(zone.Bulle!.Contour.Count >= 3);
            }

            sortie.WriteLine($"{avecBulle} bulles retrouvées sur {lecture.Zones.Count} blocs.");
        }

        /// <summary>
        /// Attache au test une image de ce que la lecture a détecté, visible
        /// directement dans l'explorateur de tests.
        /// </summary>
        /// <remarks>
        /// Ce test ne vérifie rien et ne doit jamais échouer : il n'y a pas d'égalité à
        /// contrôler sur une détection. Son produit est l'aperçu, qui permet de voir
        /// d'un coup d'œil ce qui a été trouvé sans quitter Visual Studio — et
        /// notamment de comprendre pourquoi l'un des trois autres tests est tombé.
        /// </remarks>
        [Fact]
        public void Lecture_AttacheUnApercuDeLaDetection()
        {
            TestContext.Current.AddAttachment(
                "apercu-detection", DessinerLApercu(), "image/png");

            sortie.WriteLine("Aperçu attaché : vert = quadrilatère du texte, rouge = contour de la bulle.");
        }

        private byte[] DessinerLApercu()
        {
            using Mat dessin = Cv2.ImDecode(lecture.Originale.Image, ImreadModes.Color);

            for (int rang = 0; rang < lecture.Zones.Count; rang++)
            {
                ZoneDeTexte zone = lecture.Zones[rang];

                DessinerLaBulle(dessin, zone.Bulle);
                DessinerLeQuadrilatere(dessin, zone.Rectangle);
                DessinerLeRang(dessin, zone.Rectangle, rang);
            }

            return dessin.ImEncode(".png");
        }

        private static void DessinerLaBulle(Mat dessin, Bulle? bulle)
        {
            if (bulle == null || bulle.Contour.Count < 3)
            {
                return;
            }

            Point[] contour = bulle.Contour.Select(Vers).ToArray();

            Cv2.Polylines(dessin, new[] { contour }, isClosed: true, new Scalar(0, 0, 255), 5);
        }

        private static void DessinerLeQuadrilatere(Mat dessin, Quadrilatere quadrilatere)
        {
            Point[] coins =
            {
                Vers(quadrilatere.HautGauche),
                Vers(quadrilatere.HautDroit),
                Vers(quadrilatere.BasDroit),
                Vers(quadrilatere.BasGauche)
            };

            Cv2.Polylines(dessin, new[] { coins }, isClosed: true, new Scalar(0, 200, 0), 4);
        }

        private static void DessinerLeRang(Mat dessin, Quadrilatere quadrilatere, int rang)
        {
            Point ancre = Vers(quadrilatere.HautGauche);

            Cv2.PutText(
                dessin,
                rang.ToString(),
                new Point(ancre.X, Math.Max(40, ancre.Y - 14)),
                HersheyFonts.HersheySimplex,
                1.4,
                new Scalar(255, 60, 0),
                4);
        }

        private static Point Vers(Coordonnee point)
        {
            return new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
        }

        private void Decrire()
        {
            sortie.WriteLine($"Planche       : {PlancheDEssai.Nom}");
            sortie.WriteLine($"Blocs lus     : {lecture.Zones.Count}");
            sortie.WriteLine($"Avec bulle    : {lecture.Zones.Count(zone => zone.Bulle != null)} sur {lecture.Zones.Count}");
            sortie.WriteLine($"Confiance moy.: {lecture.Zones.Average(zone => zone.Confiance):0.###}");
            sortie.WriteLine(string.Empty);

            foreach (ZoneDeTexte zone in lecture.Zones)
            {
                string bulle = zone.Bulle == null ? "sans bulle" : $"{zone.Bulle.Contour.Count} pts";
                string ligne = zone.HauteurDeLigne == null ? "?" : $"{zone.HauteurDeLigne.Value:0} px";

                sortie.WriteLine(
                    $"[{zone.Confiance:0.00}] ({bulle,9}) {zone.Angle,6:0.0}° ligne {ligne,6}  {zone.TexteOriginal}");
            }
        }
    }
}
