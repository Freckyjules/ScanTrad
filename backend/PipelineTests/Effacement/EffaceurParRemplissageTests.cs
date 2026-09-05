using OpenCvSharp;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Effacement;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Effacement
{
    /// <summary>
    /// Vérifie le remplissage des bulles sur une image fabriquée pour l'occasion.
    /// </summary>
    /// <remarks>
    /// L'image est construite en mémoire — un fond sombre uni — plutôt que lue depuis
    /// un fichier : le test reste rapide, et surtout on sait exactement ce que vaut
    /// chaque pixel avant et après.
    /// </remarks>
    public class EffaceurParRemplissageTests
    {
        /// <summary>
        /// L'intérieur d'une bulle est repeint en blanc.
        /// </summary>
        [Fact]
        public void Effacer_ZoneAvecBulle_RepeintLInterieurEnBlanc()
        {
            Planche planche = PlancheSombre(ZoneAvecBulle(20, 20, 60, 60));

            Planche nettoyee = new EffaceurParRemplissage().Effacer(planche);

            Assert.Equal(255, Pixel(nettoyee, 50, 50));
        }

        /// <summary>
        /// Ce qui est hors de la bulle n'est pas touché.
        /// </summary>
        [Fact]
        public void Effacer_HorsDeLaBulle_NeToucheARien()
        {
            Planche planche = PlancheSombre(ZoneAvecBulle(20, 20, 60, 60));

            Planche nettoyee = new EffaceurParRemplissage().Effacer(planche);

            Assert.Equal(0, Pixel(nettoyee, 5, 5));
            Assert.Equal(0, Pixel(nettoyee, 95, 95));
        }

        /// <summary>
        /// Une zone sans bulle est laissée intacte : sans contour, on ne sait pas
        /// jusqu'où effacer sans mordre sur le dessin.
        /// </summary>
        [Fact]
        public void Effacer_ZoneSansBulle_LaisseLImageIntacte()
        {
            ZoneDeTexte onomatopee = new ZoneDeTexte();
            onomatopee.Rectangle = Quadrilatere.DepuisRectangle(20, 20, 60, 60);
            onomatopee.TexteOriginal = "BEAM";

            Planche nettoyee = new EffaceurParRemplissage().Effacer(PlancheSombre(onomatopee));

            Assert.Equal(0, Pixel(nettoyee, 50, 50));
        }

        /// <summary>
        /// L'image d'origine n'est pas modifiée. C'est l'invariant qui porte toute la
        /// boucle de correction : sans elle, impossible de tout relancer si la
        /// détection s'est mal passée.
        /// </summary>
        [Fact]
        public void Effacer_NeModifiePasLaPlancheRecue()
        {
            Planche planche = PlancheSombre(ZoneAvecBulle(20, 20, 60, 60));
            byte[] avant = planche.Image.ToArray();

            Planche nettoyee = new EffaceurParRemplissage().Effacer(planche);

            Assert.NotSame(planche, nettoyee);
            Assert.Equal(avant, planche.Image);
            Assert.Equal(0, Pixel(planche, 50, 50));
        }

        /// <summary>
        /// L'effacement ne touche ni aux zones ni au sens de lecture.
        /// </summary>
        [Fact]
        public void Effacer_ConserveLesZonesEtLeSens()
        {
            ZoneDeTexte zone = ZoneAvecBulle(20, 20, 60, 60);
            zone.TexteOriginal = "YOU CAN'T";

            Planche planche = new Planche(
                ImageSombre(), SensDeLecture.GaucheADroite, new[] { zone });

            Planche nettoyee = new EffaceurParRemplissage().Effacer(planche);

            Assert.Equal(SensDeLecture.GaucheADroite, nettoyee.Sens);
            Assert.Equal("YOU CAN'T", Assert.Single(nettoyee.Zones).TexteOriginal);
        }

        /// <summary>
        /// La couleur de repli se choisit : toutes les bulles ne sont pas blanches,
        /// celles des pensées ou des cris sont parfois sombres.
        /// </summary>
        [Fact]
        public void Effacer_SansFondMesure_UtiliseLaCouleurDeRepli()
        {
            Planche planche = PlancheSombre(ZoneAvecBulle(20, 20, 60, 60));

            Planche nettoyee = new EffaceurParRemplissage(Scalar.All(128)).Effacer(planche);

            Assert.Equal(128, Pixel(nettoyee, 50, 50));
        }

        /// <summary>
        /// Quand la lecture a mesuré le fond de la bulle, c'est de cette couleur-là
        /// qu'on repeint.
        /// </summary>
        /// <remarks>
        /// Les trois composantes sont vérifiées séparément : les inverser passerait
        /// inaperçu sur un gris, alors que le modèle nomme ses composantes en rouge,
        /// vert, bleu et qu'OpenCV les range dans l'autre sens.
        /// </remarks>
        [Fact]
        public void Effacer_ZoneAvecUnFondMesure_RepeintDeCetteCouleur()
        {
            ZoneDeTexte zone = ZoneAvecBulle(20, 20, 60, 60);
            zone.CouleurDeFond = new Couleur(250, 240, 230);

            Planche nettoyee = new EffaceurParRemplissage().Effacer(PlancheSombre(zone));

            Vec3b pixel = PixelCouleur(nettoyee, 50, 50);

            Assert.Equal(230, pixel.Item0);
            Assert.Equal(240, pixel.Item1);
            Assert.Equal(250, pixel.Item2);
        }

        /// <summary>
        /// Le fond mesuré l'emporte sur la couleur réglée : la planche en sait plus
        /// long qu'un réglage posé à l'avance.
        /// </summary>
        [Fact]
        public void Effacer_UnFondMesure_PrimeSurLaCouleurDeRepli()
        {
            ZoneDeTexte zone = ZoneAvecBulle(20, 20, 60, 60);
            zone.CouleurDeFond = new Couleur(200, 200, 200);

            Planche nettoyee = new EffaceurParRemplissage(Scalar.All(128)).Effacer(PlancheSombre(zone));

            Assert.Equal(200, Pixel(nettoyee, 50, 50));
        }

        /// <summary>
        /// Une planche sans image n'a rien à effacer et doit être signalée.
        /// </summary>
        [Fact]
        public void Effacer_ImageVide_LeveArgumentException()
        {
            Planche planche = new Planche(Array.Empty<byte>());

            Assert.Throws<ArgumentException>(() => new EffaceurParRemplissage().Effacer(planche));
        }

        /// <summary>
        /// Des octets qui ne forment pas une image doivent être signalés, plutôt que
        /// de faire échouer une étape plus loin sur un message incompréhensible.
        /// </summary>
        [Fact]
        public void Effacer_ImageNonDecodable_LeveArgumentException()
        {
            Planche planche = new Planche(new byte[] { 1, 2, 3, 4 });

            Assert.Throws<ArgumentException>(() => new EffaceurParRemplissage().Effacer(planche));
        }

        /// <summary>
        /// Effacer rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void Effacer_Null_LeveArgumentNullException()
        {
            IEffaceurDeTexte effaceur = new EffaceurParRemplissage();

            Assert.Throws<ArgumentNullException>(() => effaceur.Effacer(null!));
        }

        private static Planche PlancheSombre(ZoneDeTexte zone)
        {
            return new Planche(ImageSombre(), SensDeLecture.DroiteAGauche, new[] { zone });
        }

        private static byte[] ImageSombre()
        {
            // Un carré de 100 sur 100, entièrement noir : n'importe quel pixel devenu
            // clair ne peut venir que de l'effacement.
            using Mat fond = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));

            return fond.ImEncode(".png");
        }

        private static ZoneDeTexte ZoneAvecBulle(double x, double y, double largeur, double hauteur)
        {
            ZoneDeTexte zone = new ZoneDeTexte();

            zone.Rectangle = Quadrilatere.DepuisRectangle(x, y, largeur, hauteur);
            zone.TexteOriginal = "texte";
            zone.Bulle = new Bulle(new List<Coordonnee>
            {
                new Coordonnee(x, y),
                new Coordonnee(x + largeur, y),
                new Coordonnee(x + largeur, y + hauteur),
                new Coordonnee(x, y + hauteur)
            });

            return zone;
        }

        private static int Pixel(Planche planche, int x, int y)
        {
            return PixelCouleur(planche, x, y).Item0;
        }

        private static Vec3b PixelCouleur(Planche planche, int x, int y)
        {
            using Mat image = Cv2.ImDecode(planche.Image, ImreadModes.Color);

            // OpenCV range les composantes en bleu-vert-rouge : Item0 est le bleu.
            return image.At<Vec3b>(y, x);
        }
    }
}
