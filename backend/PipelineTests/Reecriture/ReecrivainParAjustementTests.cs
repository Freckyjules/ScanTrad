using System.Runtime.Versioning;
using OpenCvSharp;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.Reecriture;

namespace ScanTrad.PipelineTests.Reecriture
{
    /// <summary>
    /// Vérifie la réécriture du texte traduit sur une image fabriquée pour l'occasion.
    /// </summary>
    /// <remarks>
    /// L'image est un fond blanc uni construit en mémoire, comme pour l'effacement :
    /// le test reste rapide et on sait exactement ce que vaut chaque pixel avant
    /// rendu. Aucune assertion ne vérifie qu'une lettre est bien formée — c'est
    /// l'affaire d'un humain, via le test sur planche réelle — seulement que de
    /// l'encre est bien posée à l'intérieur de la bulle, avec la bonne couleur.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class ReecrivainParAjustementTests
    {
        /// <summary>
        /// Une zone traduite avec une bulle connue fait apparaître de l'encre à
        /// l'intérieur de la boîte.
        /// </summary>
        [Fact]
        public void Reecrire_ZoneAvecTraductionEtBulle_DessineDuTexteDansLaBulle()
        {
            Planche planche = PlancheBlanche(ZoneAvecBulle(20, 20, 160, 160, "I", 80));

            Planche composee = new ReecrivainParAjustement().Reecrire(planche);

            Assert.True(SommeMinimale(composee) < 200, "Aucune encre détectée dans l'image.");
        }

        /// <summary>
        /// Une traduction plus longue que ce qui tiendrait à la taille d'origine force
        /// une réduction de police, jusqu'à ce que le texte ne morde plus le bas de la
        /// bulle.
        /// </summary>
        /// <remarks>
        /// C'est le principe demandé : partir de la taille du texte d'origine, et ne
        /// réduire que si nécessaire — jamais tronquer, jamais déborder en bas.
        /// </remarks>
        [Fact]
        public void Reecrire_TraductionPlusLongue_ReduitLaPoliceSansDeborderDeLaBulle()
        {
            string texteLong = string.Join(
                " ", Enumerable.Repeat("un texte français bien plus long que l'original", 6));

            ZoneDeTexte zone = ZoneAvecBulle(20, 20, 160, 160, texteLong, 80);
            Planche planche = PlancheBlanche(zone);

            Planche composee = new ReecrivainParAjustement().Reecrire(planche);

            (int minY, int maxY) = EtenduesVerticalesDeLEncre(composee);

            Assert.True(minY <= maxY, "Aucune encre détectée malgré un texte long.");

            // Aucune marge : la boîte de texte est le rectangle tel quel (20..180),
            // pas le contour de la bulle. Une petite tolérance absorbe
            // l'antialiasing du contour des lettres.
            double basDeLaBoite = 20 + 160;

            Assert.True(
                maxY <= basDeLaBoite + 5,
                $"L'encre descend jusqu'à {maxY}px, au-delà du bas de la boîte ({basDeLaBoite:0}px) : " +
                "le texte mord le bas de la bulle au lieu d'avoir réduit sa police.");
        }

        /// <summary>
        /// Le texte se pose dans le rectangle, pas dans le contour de la bulle : une
        /// bulle bien plus grande que le rectangle ne doit pas laisser l'encre
        /// déborder du rectangle.
        /// </summary>
        /// <remarks>
        /// C'est le cadrage, en amont, qui a la charge de maximiser le rectangle dans
        /// la bulle. La réécriture n'a plus à regarder le contour de la bulle du
        /// tout : ce test construit délibérément une bulle bien plus grande que le
        /// rectangle pour vérifier qu'elle n'influe pas sur la mise en page.
        /// </remarks>
        [Fact]
        public void Reecrire_BulleBienPlusGrandeQueLeRectangle_NeDeborsePasDuRectangle()
        {
            ZoneDeTexte zone = ZoneAvecBulle(180, 180, 40, 40, "I", 30);
            zone.Bulle = new Bulle(Carre(0, 0, 400, 400));

            Planche composee = new ReecrivainParAjustement().Reecrire(PlancheBlanche(zone));

            (int minY, int maxY) = EtenduesVerticalesDeLEncre(composee);

            Assert.True(minY <= maxY, "Aucune encre détectée.");
            Assert.True(minY >= 175, $"L'encre commence à {minY}px, au-dessus du rectangle (180px).");
            Assert.True(maxY <= 225, $"L'encre descend à {maxY}px, en dessous du rectangle (220px).");
        }

        /// <summary>
        /// Une zone à réécrire sans hauteur de ligne mesurée est un signe qu'une étape
        /// d'avant a un problème : mieux vaut le signaler que deviner une taille.
        /// </summary>
        [Fact]
        public void Reecrire_ZoneAvecBulleSansHauteurDeLigne_LeveInvalidOperationException()
        {
            ZoneDeTexte zone = ZoneAvecBulle(20, 20, 160, 160, "traduit", null);

            Planche planche = PlancheBlanche(zone);

            Assert.Throws<InvalidOperationException>(() => new ReecrivainParAjustement().Reecrire(planche));
        }

        /// <summary>
        /// Une zone sans bulle est laissée intacte : sans contour, on ne sait pas où
        /// poser le texte.
        /// </summary>
        [Fact]
        public void Reecrire_ZoneSansBulle_NeDessineRien()
        {
            ZoneDeTexte onomatopee = new ZoneDeTexte();
            onomatopee.Rectangle = Quadrilatere.DepuisRectangle(20, 20, 160, 160);
            onomatopee.TexteOriginal = "BEAM";
            onomatopee.TexteTraduit = "BOUM";

            Planche composee = new ReecrivainParAjustement().Reecrire(PlancheBlanche(onomatopee));

            Assert.Equal(765, SommeMinimale(composee));
        }

        /// <summary>
        /// Une traduction à <c>null</c> n'est pas encore faite : rien ne doit être
        /// écrit à sa place.
        /// </summary>
        [Fact]
        public void Reecrire_TraductionNulle_NeDessineRien()
        {
            ZoneDeTexte zone = ZoneAvecBulle(20, 20, 160, 160, null, 80);

            Planche composee = new ReecrivainParAjustement().Reecrire(PlancheBlanche(zone));

            Assert.Equal(765, SommeMinimale(composee));
        }

        /// <summary>
        /// Une traduction vide veut dire « traduite par rien » : la bulle nettoyée
        /// reste telle quelle, comme pour une traduction non encore faite.
        /// </summary>
        [Fact]
        public void Reecrire_TraductionVide_NeDessineRien()
        {
            ZoneDeTexte zone = ZoneAvecBulle(20, 20, 160, 160, string.Empty, 80);

            Planche composee = new ReecrivainParAjustement().Reecrire(PlancheBlanche(zone));

            Assert.Equal(765, SommeMinimale(composee));
        }

        /// <summary>
        /// L'image reçue n'est pas modifiée : c'est ce qui permet de rejouer la
        /// composition avec une traduction corrigée sans repartir de l'effacement.
        /// </summary>
        [Fact]
        public void Reecrire_NeModifiePasLaPlancheRecue()
        {
            Planche planche = PlancheBlanche(ZoneAvecBulle(20, 20, 160, 160, "I", 80));
            byte[] avant = planche.Image.ToArray();

            Planche composee = new ReecrivainParAjustement().Reecrire(planche);

            Assert.NotSame(planche, composee);
            Assert.Equal(avant, planche.Image);
            Assert.Equal(765, SommeMinimale(planche));
        }

        /// <summary>
        /// La réécriture ne touche ni aux zones ni au sens de lecture.
        /// </summary>
        [Fact]
        public void Reecrire_ConserveLesZonesEtLeSens()
        {
            ZoneDeTexte zone = ZoneAvecBulle(20, 20, 160, 160, "I", 80);
            zone.TexteOriginal = "YOU CAN'T";

            Planche planche = new Planche(ImageBlanche(), SensDeLecture.GaucheADroite, new[] { zone });

            Planche composee = new ReecrivainParAjustement().Reecrire(planche);

            Assert.Equal(SensDeLecture.GaucheADroite, composee.Sens);
            Assert.Equal("YOU CAN'T", Assert.Single(composee.Zones).TexteOriginal);
        }

        /// <summary>
        /// La couleur du texte se choisit. Avec un rouge pur sur fond blanc, la
        /// composante rouge d'un pixel encré ne peut pas bouger — elle vaut déjà 255
        /// des deux côtés du mélange — seules le vert et le bleu chutent : c'est une
        /// signature qu'aucun autre réglage ne produit.
        /// </summary>
        [Fact]
        public void Reecrire_CouleurConfiguree_RespecteLaCouleur()
        {
            Planche planche = PlancheBlanche(ZoneAvecBulle(20, 20, 160, 160, "I", 80));

            ReecrivainParAjustement reecrivain = new ReecrivainParAjustement("Arial", new Couleur(255, 0, 0));
            Planche composee = reecrivain.Reecrire(planche);

            Vec3b pixel = PixelLePlusEncreParVert(composee);

            Assert.True(pixel.Item1 < 50, "Le vert aurait dû chuter là où le rouge a été posé.");
            Assert.True(pixel.Item2 > 250, "Le rouge ne devrait pas bouger : il vaut déjà 255 sur fond blanc.");
        }

        /// <summary>
        /// Une planche sans image n'a rien à composer et doit être signalée.
        /// </summary>
        [Fact]
        public void Reecrire_ImageVide_LeveArgumentException()
        {
            Planche planche = new Planche(Array.Empty<byte>());

            Assert.Throws<ArgumentException>(() => new ReecrivainParAjustement().Reecrire(planche));
        }

        /// <summary>
        /// Des octets qui ne forment pas une image doivent être signalés, plutôt que
        /// de faire échouer GDI+ plus loin sur un message incompréhensible.
        /// </summary>
        [Fact]
        public void Reecrire_ImageNonDecodable_LeveArgumentException()
        {
            Planche planche = new Planche(new byte[] { 1, 2, 3, 4 });

            Assert.Throws<ArgumentException>(() => new ReecrivainParAjustement().Reecrire(planche));
        }

        /// <summary>
        /// Réécrire rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void Reecrire_Null_LeveArgumentNullException()
        {
            IReecrivainDeTexte reecrivain = new ReecrivainParAjustement();

            Assert.Throws<ArgumentNullException>(() => reecrivain.Reecrire(null!));
        }

        /// <summary>
        /// Une police à <c>null</c> n'a pas de sens et doit être signalée.
        /// </summary>
        [Fact]
        public void Constructeur_NomDePoliceNull_LeveArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ReecrivainParAjustement(null!, new Couleur()));
        }

        /// <summary>
        /// Une police vide n'a pas de sens et doit être signalée.
        /// </summary>
        [Fact]
        public void Constructeur_NomDePoliceVide_LeveArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ReecrivainParAjustement("   ", new Couleur()));
        }

        /// <summary>
        /// Une couleur à <c>null</c> n'a pas de sens et doit être signalée.
        /// </summary>
        [Fact]
        public void Constructeur_CouleurNull_LeveArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ReecrivainParAjustement("Arial", null!));
        }

        private static Planche PlancheBlanche(ZoneDeTexte zone)
        {
            return new Planche(ImageBlanche(), SensDeLecture.DroiteAGauche, new[] { zone });
        }

        private static byte[] ImageBlanche()
        {
            // Un carré de 400 sur 400, entièrement blanc : n'importe quel pixel
            // devenu sombre ne peut venir que de la réécriture.
            using Mat fond = new Mat(400, 400, MatType.CV_8UC3, Scalar.All(255));

            return fond.ImEncode(".png");
        }

        private static ZoneDeTexte ZoneAvecBulle(
            double x, double y, double largeur, double hauteur, string? texteTraduit, double? hauteurDeLigne)
        {
            ZoneDeTexte zone = new ZoneDeTexte();

            zone.Rectangle = Quadrilatere.DepuisRectangle(x, y, largeur, hauteur);
            zone.TexteOriginal = "texte";
            zone.TexteTraduit = texteTraduit;
            zone.HauteurDeLigne = hauteurDeLigne;
            zone.Bulle = new Bulle(new List<Coordonnee>
            {
                new Coordonnee(x, y),
                new Coordonnee(x + largeur, y),
                new Coordonnee(x + largeur, y + hauteur),
                new Coordonnee(x, y + hauteur)
            });

            return zone;
        }

        private static Coordonnee[] Carre(double x, double y, double largeur, double hauteur)
        {
            return new[]
            {
                new Coordonnee(x, y),
                new Coordonnee(x + largeur, y),
                new Coordonnee(x + largeur, y + hauteur),
                new Coordonnee(x, y + hauteur)
            };
        }

        private static int SommeMinimale(Planche planche)
        {
            using Mat image = Cv2.ImDecode(planche.Image, ImreadModes.Color);

            int minimum = 765;

            for (int y = 0; y < image.Rows; y++)
            {
                for (int x = 0; x < image.Cols; x++)
                {
                    Vec3b pixel = image.At<Vec3b>(y, x);
                    int somme = pixel.Item0 + pixel.Item1 + pixel.Item2;

                    if (somme < minimum)
                    {
                        minimum = somme;
                    }
                }
            }

            return minimum;
        }

        private static (int minY, int maxY) EtenduesVerticalesDeLEncre(Planche planche)
        {
            using Mat image = Cv2.ImDecode(planche.Image, ImreadModes.Color);

            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int y = 0; y < image.Rows; y++)
            {
                for (int x = 0; x < image.Cols; x++)
                {
                    Vec3b pixel = image.At<Vec3b>(y, x);

                    if (pixel.Item0 + pixel.Item1 + pixel.Item2 < 700)
                    {
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            return (minY, maxY);
        }

        private static Vec3b PixelLePlusEncreParVert(Planche planche)
        {
            using Mat image = Cv2.ImDecode(planche.Image, ImreadModes.Color);

            Vec3b resultat = new Vec3b(255, 255, 255);
            int minimum = 255;

            for (int y = 0; y < image.Rows; y++)
            {
                for (int x = 0; x < image.Cols; x++)
                {
                    Vec3b pixel = image.At<Vec3b>(y, x);

                    if (pixel.Item1 < minimum)
                    {
                        minimum = pixel.Item1;
                        resultat = pixel;
                    }
                }
            }

            return resultat;
        }
    }
}
