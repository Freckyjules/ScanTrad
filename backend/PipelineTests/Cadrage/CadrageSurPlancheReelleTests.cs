using OpenCvSharp;
using ScanTrad.Pipeline.Cadrage;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Cadrage
{
    /// <summary>
    /// Maximise les rectangles d'une vraie planche déjà lue, et attache un aperçu
    /// montrant, pour chaque bulle, le rectangle d'origine et le rectangle maximisé
    /// côte à côte — pour qu'un humain juge le gain à l'œil.
    /// </summary>
    /// <remarks>
    /// La lecture ne se refait pas ici : elle vient de
    /// <see cref="LectureDeLaPlancheDEssai"/>, partagée par toute la collection
    /// d'intégration. Comme pour l'ordre de lecture, cette étape n'est que de la
    /// géométrie sur les zones déjà lues — elle ne coûte rien et n'a pas besoin de
    /// relire l'image.
    /// <para>
    /// L'aperçu ne réutilise pas <c>ApercuDePlanche</c> : celui-ci ne dessine qu'un
    /// seul rectangle par zone, alors que le propos ici est justement de comparer
    /// l'ancien et le nouveau. Le rendu reste proche du sien — même contour rouge pour
    /// la bulle — pour rester lisible par quelqu'un qui a déjà vu l'aperçu de
    /// détection.
    /// </para>
    /// <para>
    /// Aucune assertion ne peut dire qu'un rectangle est <em>mieux</em> cadré : c'est
    /// à l'aperçu de le montrer. Le seul invariant vérifiable, et le plus important,
    /// est que le rectangle maximisé ne sorte jamais du contour de sa bulle — c'est
    /// tout ce que cette étape promet.
    /// </para>
    /// </remarks>
    [Trait("Categorie", "Integration")]
    [Collection(CollectionDIntegration.Nom)]
    public class CadrageSurPlancheReelleTests
    {
        private static readonly Scalar CouleurDeLaBulle = new Scalar(0, 0, 255);
        private static readonly Scalar CouleurDAvant = new Scalar(0, 140, 255);
        private static readonly Scalar CouleurDApres = new Scalar(0, 200, 0);

        private readonly LectureDeLaPlancheDEssai lecture;
        private readonly ITestOutputHelper sortie;

        /// <summary>
        /// Initialise le test avec la lecture partagée et le collecteur de sortie.
        /// </summary>
        /// <param name="lecture">La planche d'essai, déjà lue.</param>
        /// <param name="sortie">Le canal où écrire ce qu'on veut voir apparaître.</param>
        public CadrageSurPlancheReelleTests(LectureDeLaPlancheDEssai lecture, ITestOutputHelper sortie)
        {
            this.lecture = lecture;
            this.sortie = sortie;
        }

        /// <summary>
        /// Maximise les rectangles de la planche d'essai, vérifie qu'aucun ne sort de
        /// sa bulle, et attache l'aperçu avant/après.
        /// </summary>
        [Fact]
        public void Ajuster_SurUnePlancheReelle_GardeChaqueRectangleDansSaBulle()
        {
            Planche lue = lecture.Copier(SensDeLecture.GaucheADroite);

            // Capturés avant l'ajustement : l'implémentation modifie les zones sur
            // place, et le rectangle d'origine ne se retrouverait plus après coup.
            List<Quadrilatere> avant = lue.Zones.Select(zone => zone.Rectangle).ToList();

            Planche ajustee = new AjusteurDeRectangleParRasterisation().Ajuster(lue);

            int avecBulle = ajustee.Zones.Count(zone => zone.Bulle != null);

            Assert.True(avecBulle > 0, "Aucune bulle à cadrer : le test ne prouverait rien.");

            foreach (ZoneDeTexte zone in ajustee.Zones)
            {
                if (zone.Bulle == null)
                {
                    continue;
                }

                // Un rectangle maximal a le droit de toucher le contour de sa bulle —
                // c'est même attendu, sinon il ne serait pas maximal. Ce qu'il n'a pas
                // le droit de faire, c'est de le dépasser : le test veut un « dedans
                // ou sur le contour » (≤), pas un « strictement dedans » (<). Or
                // Bulle.Contient est un test de bord ambigu (sa propre documentation
                // le dit), donc on vérifie plutôt la distance au segment le plus
                // proche du contour.
                Assert.True(
                    EstDansOuSurLaBulle(zone.Bulle, zone.Rectangle.HautGauche), Deborde(zone, "haut-gauche"));
                Assert.True(
                    EstDansOuSurLaBulle(zone.Bulle, zone.Rectangle.HautDroit), Deborde(zone, "haut-droit"));
                Assert.True(
                    EstDansOuSurLaBulle(zone.Bulle, zone.Rectangle.BasGauche), Deborde(zone, "bas-gauche"));
                Assert.True(
                    EstDansOuSurLaBulle(zone.Bulle, zone.Rectangle.BasDroit), Deborde(zone, "bas-droit"));
            }

            byte[] apercu = DessinerAvantApres(ajustee.Image, ajustee.Zones, avant);

            TestContext.Current.AddAttachment("apercu-cadrage", apercu, "image/png");

            string fichier = Deposer(apercu, "Akashic-cadrage.png");

            Decrire(avant, ajustee.Zones, fichier);
        }

        private static string Deborde(ZoneDeTexte zone, string coin)
        {
            return $"Le coin {coin} du rectangle de « {zone.TexteOriginal} » sort de sa bulle.";
        }

        // Tolérance de rasterisation : un coin issu de la grille de pixels du masque
        // peut se retrouver jusqu'à ~1,4 px du contour continu de la bulle (la
        // diagonale d'un pixel) sans que ça veuille dire qu'il en est sorti.
        private const double ToleranceDeRasterisation = 1.5;

        private static bool EstDansOuSurLaBulle(Bulle bulle, Coordonnee point)
        {
            return bulle.Contient(point) || DistanceAuContour(bulle, point) <= ToleranceDeRasterisation;
        }

        private static double DistanceAuContour(Bulle bulle, Coordonnee point)
        {
            IList<Coordonnee> contour = bulle.Contour;
            double minimum = double.MaxValue;

            for (int i = 0; i < contour.Count; i++)
            {
                Coordonnee a = contour[i];
                Coordonnee b = contour[(i + 1) % contour.Count];

                minimum = Math.Min(minimum, DistanceAuSegment(point, a, b));
            }

            return minimum;
        }

        private static double DistanceAuSegment(Coordonnee point, Coordonnee a, Coordonnee b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double longueurCarree = (dx * dx) + (dy * dy);

            if (longueurCarree < 1e-9)
            {
                return point.DistanceVers(a);
            }

            double t = (((point.X - a.X) * dx) + ((point.Y - a.Y) * dy)) / longueurCarree;
            t = Math.Clamp(t, 0, 1);

            Coordonnee projection = new Coordonnee(a.X + (t * dx), a.Y + (t * dy));

            return point.DistanceVers(projection);
        }

        private static byte[] DessinerAvantApres(
            byte[] image, IReadOnlyList<ZoneDeTexte> zones, IReadOnlyList<Quadrilatere> avant)
        {
            using Mat dessin = Cv2.ImDecode(image, ImreadModes.Color);

            for (int i = 0; i < zones.Count; i++)
            {
                ZoneDeTexte zone = zones[i];

                if (zone.Bulle == null || zone.Bulle.Contour.Count < 3)
                {
                    continue;
                }

                DessinerContour(dessin, zone.Bulle.Contour, CouleurDeLaBulle);
                DessinerRectangle(dessin, avant[i], CouleurDAvant, 3);
                DessinerRectangle(dessin, zone.Rectangle, CouleurDApres, 3);
            }

            return dessin.ImEncode(".png");
        }

        private static void DessinerContour(Mat dessin, IList<Coordonnee> contour, Scalar couleur)
        {
            Point[] points = contour.Select(Vers).ToArray();

            Cv2.Polylines(dessin, new[] { points }, isClosed: true, couleur, 3);
        }

        private static void DessinerRectangle(Mat dessin, Quadrilatere rectangle, Scalar couleur, int epaisseur)
        {
            Point[] coins =
            {
                Vers(rectangle.HautGauche),
                Vers(rectangle.HautDroit),
                Vers(rectangle.BasDroit),
                Vers(rectangle.BasGauche)
            };

            Cv2.Polylines(dessin, new[] { coins }, isClosed: true, couleur, epaisseur);
        }

        private static Point Vers(Coordonnee point)
        {
            return new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
        }

        private static string Deposer(byte[] image, string nom)
        {
            string dossier = Path.Combine(AppContext.BaseDirectory, "sorties");

            Directory.CreateDirectory(dossier);

            string fichier = Path.Combine(dossier, nom);

            File.WriteAllBytes(fichier, image);

            return fichier;
        }

        private void Decrire(IReadOnlyList<Quadrilatere> avant, IReadOnlyList<ZoneDeTexte> apres, string fichier)
        {
            sortie.WriteLine($"Planche : {PlancheDEssai.Nom}");
            sortie.WriteLine(string.Empty);
            sortie.WriteLine("Aperçu attaché : bulle en rouge, rectangle d'origine en orange, " +
                              "rectangle maximisé en vert.");
            sortie.WriteLine($"Déposé ici : {fichier}");
            sortie.WriteLine(string.Empty);

            for (int i = 0; i < apres.Count; i++)
            {
                double aireAvant = avant[i].Largeur * avant[i].Hauteur;
                double aireApres = apres[i].Rectangle.Largeur * apres[i].Rectangle.Hauteur;
                double variation = aireAvant == 0 ? 0 : ((aireApres / aireAvant) - 1) * 100;

                sortie.WriteLine(
                    $"« {apres[i].TexteOriginal} » : aire {aireAvant:0} -> {aireApres:0} px² " +
                    $"({variation:+0;-0}%)");
            }
        }
    }
}
