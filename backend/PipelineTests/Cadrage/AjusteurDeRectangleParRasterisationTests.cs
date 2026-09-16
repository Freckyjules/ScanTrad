using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Cadrage;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Cadrage
{
    /// <summary>
    /// Vérifie la maximisation du rectangle sur des contours de bulle écrits à la
    /// main : un carré, dont le résultat attendu se calcule à l'œil, et un cercle
    /// approché, dont le plus grand rectangle inscrit a une formule connue
    /// (2·r² pour un cercle de rayon r) qui sert d'oracle indépendant de
    /// l'implémentation.
    /// </summary>
    public class AjusteurDeRectangleParRasterisationTests
    {
        /// <summary>
        /// Une zone sans bulle n'a rien à maximiser : le rectangle du détecteur reste
        /// tel quel.
        /// </summary>
        [Fact]
        public void Ajuster_ZoneSansBulle_NeChangeRienAuRectangle()
        {
            ZoneDeTexte zone = new ZoneDeTexte();
            zone.Rectangle = Quadrilatere.DepuisRectangle(10, 10, 40, 20);
            zone.TexteOriginal = "BEAM";

            Planche planche = new Planche(ImageBidon(), SensDeLecture.DroiteAGauche, new[] { zone });

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(planche);

            Quadrilatere rectangle = Assert.Single(resultat.Zones).Rectangle;
            Assert.Equal(10, rectangle.HautGauche.X, 3);
            Assert.Equal(10, rectangle.HautGauche.Y, 3);
            Assert.Equal(40, rectangle.Largeur, 3);
            Assert.Equal(20, rectangle.Hauteur, 3);
        }

        /// <summary>
        /// Sur une bulle carrée, le plus grand rectangle inscrit est le carré
        /// lui-même : c'est le cas le plus simple à vérifier à l'œil.
        /// </summary>
        [Fact]
        public void Ajuster_BulleCarree_TrouveLeCarreEntier()
        {
            ZoneDeTexte zone = ZoneAvecBulle(Carre(20, 20, 100, 100));

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(
                new Planche(ImageBidon(), SensDeLecture.DroiteAGauche, new[] { zone }));

            Quadrilatere rectangle = Assert.Single(resultat.Zones).Rectangle;

            // À un pixel près : la rasterisation arrondit le contour à la grille.
            Assert.True(Math.Abs(rectangle.Largeur - 100) <= 1, $"Largeur = {rectangle.Largeur}");
            Assert.True(Math.Abs(rectangle.Hauteur - 100) <= 1, $"Hauteur = {rectangle.Hauteur}");
        }

        /// <summary>
        /// Sur un cercle de rayon 100, le plus grand rectangle inscrit est un carré
        /// dont l'aire vaut 2·r² — une formule connue, indépendante de
        /// l'implémentation testée, qui sert d'oracle.
        /// </summary>
        [Fact]
        public void Ajuster_BulleCirculaire_TrouveUneAireProcheDeLaFormuleTheorique()
        {
            const double rayon = 100;

            ZoneDeTexte zone = ZoneAvecBulle(Cercle(200, 200, rayon, 360));

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(
                new Planche(ImageBidon(), SensDeLecture.DroiteAGauche, new[] { zone }));

            Quadrilatere rectangle = Assert.Single(resultat.Zones).Rectangle;

            double aireTrouvee = rectangle.Largeur * rectangle.Hauteur;
            double aireTheorique = 2 * rayon * rayon;

            // 3 % de tolérance : la rasterisation en pixels entiers et le polygone à
            // 360 côtés qui approche le cercle introduisent chacun une petite erreur.
            double ecartRelatif = Math.Abs(aireTrouvee - aireTheorique) / aireTheorique;
            Assert.True(
                ecartRelatif < 0.03,
                $"Aire trouvée {aireTrouvee:0} vs théorique {aireTheorique:0} (écart {ecartRelatif:P1})");
        }

        /// <summary>
        /// Le rectangle maximisé ne sort jamais du contour de la bulle : chacun de ses
        /// quatre coins doit rester à une distance du centre proche du rayon, quelle
        /// que soit la forme.
        /// </summary>
        /// <remarks>
        /// Le plus grand rectangle inscrit dans un cercle a ses quatre coins
        /// <em>exactement</em> sur le cercle — le cas limite que la documentation de
        /// <see cref="Bulle.Contient"/> prévient elle-même de ne pas trancher de façon
        /// fiable. La distance au centre est donc l'oracle indépendant à utiliser ici,
        /// pas <c>Contient</c>.
        /// </remarks>
        [Fact]
        public void Ajuster_BulleCirculaire_LesQuatreCoinsRestentDansLaBulle()
        {
            const double rayon = 100;
            Coordonnee centre = new Coordonnee(200, 200);

            ZoneDeTexte zone = ZoneAvecBulle(Cercle(centre.X, centre.Y, rayon, 360));

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(
                new Planche(ImageBidon(), SensDeLecture.DroiteAGauche, new[] { zone }));

            Quadrilatere rectangle = Assert.Single(resultat.Zones).Rectangle;

            // Marge de 3 px : la rasterisation en pixels entiers et le polygone à
            // 360 côtés qui approche le cercle peuvent chacun décaler un coin de
            // près d'un pixel sur chaque axe, jusqu'à ~1,4 px en diagonale.
            const double tolerance = 3.0;

            AssertDansLeCercle(centre, rayon, tolerance, rectangle.HautGauche, "haut-gauche");
            AssertDansLeCercle(centre, rayon, tolerance, rectangle.HautDroit, "haut-droit");
            AssertDansLeCercle(centre, rayon, tolerance, rectangle.BasGauche, "bas-gauche");
            AssertDansLeCercle(centre, rayon, tolerance, rectangle.BasDroit, "bas-droit");
        }

        private static void AssertDansLeCercle(
            Coordonnee centre, double rayon, double tolerance, Coordonnee point, string nom)
        {
            double distance = centre.DistanceVers(point);

            Assert.True(
                distance <= rayon + tolerance,
                $"Coin {nom} à {distance:0.##} px du centre, au-delà du rayon {rayon} (+{tolerance} de marge).");
        }

        /// <summary>
        /// Deux bulles collées, assemblées en une seule zone par la lecture, donnent
        /// un rectangle d'origine qui déborde des deux bulles à la fois (voir la
        /// remarque de l'interface). Le rectangle maximisé doit rester dans la bulle
        /// donnée, plus étroite que ce rectangle d'origine.
        /// </summary>
        [Fact]
        public void Ajuster_RectangleDOrigineDebordant_LeRamèneDansLaBulle()
        {
            ZoneDeTexte zone = ZoneAvecBulle(Carre(20, 20, 100, 100));

            // Le rectangle du détecteur, avant maximisation, déborde largement du
            // carré de la bulle (contour 20..120) : c'est le symptôme décrit par
            // l'utilisateur pour deux bulles collées assemblées en une zone.
            zone.Rectangle = Quadrilatere.DepuisRectangle(0, 0, 200, 200);

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(
                new Planche(ImageBidon(), SensDeLecture.DroiteAGauche, new[] { zone }));

            Quadrilatere rectangle = Assert.Single(resultat.Zones).Rectangle;

            Assert.True(rectangle.HautGauche.X >= 19, $"X = {rectangle.HautGauche.X}");
            Assert.True(rectangle.HautGauche.Y >= 19, $"Y = {rectangle.HautGauche.Y}");
            Assert.True(rectangle.HautGauche.X + rectangle.Largeur <= 121, $"Largeur = {rectangle.Largeur}");
            Assert.True(rectangle.HautGauche.Y + rectangle.Hauteur <= 121, $"Hauteur = {rectangle.Hauteur}");
        }

        /// <summary>
        /// Une zone sans bulle et une zone avec bulle dans la même planche : seule la
        /// seconde est modifiée.
        /// </summary>
        [Fact]
        public void Ajuster_PlusieursZones_NeModifieQueCellesAvecBulle()
        {
            ZoneDeTexte sansBulle = new ZoneDeTexte();
            sansBulle.Rectangle = Quadrilatere.DepuisRectangle(0, 0, 10, 10);
            sansBulle.TexteOriginal = "BOOM";

            ZoneDeTexte avecBulle = ZoneAvecBulle(Carre(20, 20, 100, 100));
            avecBulle.TexteOriginal = "texte";

            Planche planche = new Planche(
                ImageBidon(), SensDeLecture.DroiteAGauche, new[] { sansBulle, avecBulle });

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(planche);

            Assert.Equal(10, resultat.Zones[0].Rectangle.Largeur, 3);
            Assert.True(Math.Abs(resultat.Zones[1].Rectangle.Largeur - 100) <= 1);
        }

        /// <summary>
        /// Seul le rectangle change : le reste de la zone (texte, bulle, sens de la
        /// planche...) traverse l'étape sans y toucher.
        /// </summary>
        [Fact]
        public void Ajuster_ConserveLesAutresChampsEtLeSens()
        {
            ZoneDeTexte zone = ZoneAvecBulle(Carre(20, 20, 100, 100));
            zone.TexteOriginal = "YOU CAN'T";
            zone.TexteTraduit = "Tu ne peux pas";
            zone.Angle = 4.2;
            zone.HauteurDeLigne = 40;

            Planche planche = new Planche(ImageBidon(), SensDeLecture.GaucheADroite, new[] { zone });

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(planche);

            ZoneDeTexte apres = Assert.Single(resultat.Zones);

            Assert.Equal(SensDeLecture.GaucheADroite, resultat.Sens);
            Assert.Equal("YOU CAN'T", apres.TexteOriginal);
            Assert.Equal("Tu ne peux pas", apres.TexteTraduit);
            Assert.Equal(4.2, apres.Angle);
            Assert.Equal(40, apres.HauteurDeLigne);
        }

        /// <summary>
        /// Comme l'ordonnanceur et le traducteur, l'implémentation renseigne le
        /// résultat directement sur les zones reçues : rejouer l'étape ne dépend que
        /// du contour de la bulle, qui n'a pas changé.
        /// </summary>
        [Fact]
        public void Ajuster_ModifieLaZoneRecueEnPlace()
        {
            ZoneDeTexte zone = ZoneAvecBulle(Carre(20, 20, 100, 100));
            Planche planche = new Planche(ImageBidon(), SensDeLecture.DroiteAGauche, new[] { zone });

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(planche);

            Assert.Same(zone, Assert.Single(resultat.Zones));
        }

        /// <summary>
        /// Une bulle sans la moindre largeur garde le rectangle d'origine plutôt que
        /// d'être remplacée par une surface nulle.
        /// </summary>
        [Fact]
        public void Ajuster_BulleDegeneree_GardeLeRectangleDOrigine()
        {
            // Trois points alignés verticalement : une largeur strictement nulle,
            // donc rien à maximiser.
            ZoneDeTexte zone = ZoneAvecBulle(new[]
            {
                new Coordonnee(10, 10),
                new Coordonnee(10, 15),
                new Coordonnee(10, 20)
            });
            zone.Rectangle = Quadrilatere.DepuisRectangle(5, 5, 8, 8);

            Planche resultat = new AjusteurDeRectangleParRasterisation().Ajuster(
                new Planche(ImageBidon(), SensDeLecture.DroiteAGauche, new[] { zone }));

            Quadrilatere rectangle = Assert.Single(resultat.Zones).Rectangle;
            Assert.Equal(8, rectangle.Largeur, 3);
            Assert.Equal(8, rectangle.Hauteur, 3);
        }

        /// <summary>
        /// Ajuster rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void Ajuster_Null_LeveArgumentNullException()
        {
            IAjusteurDeRectangle ajusteur = new AjusteurDeRectangleParRasterisation();

            Assert.Throws<ArgumentNullException>(() => ajusteur.Ajuster(null!));
        }

        private static byte[] ImageBidon()
        {
            // Le rectangle ne dépend que du contour de la bulle : l'image ne sert à
            // rien ici, un octet suffit à construire une planche valide.
            return new byte[] { 0 };
        }

        private static ZoneDeTexte ZoneAvecBulle(IEnumerable<Coordonnee> contour)
        {
            ZoneDeTexte zone = new ZoneDeTexte();
            zone.Rectangle = Quadrilatere.DepuisRectangle(0, 0, 1, 1);
            zone.TexteOriginal = "texte";
            zone.Bulle = new Bulle(contour);

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

        private static Coordonnee[] Cercle(double centreX, double centreY, double rayon, int nombreDePoints)
        {
            Coordonnee[] points = new Coordonnee[nombreDePoints];

            for (int i = 0; i < nombreDePoints; i++)
            {
                double angle = 2 * Math.PI * i / nombreDePoints;
                points[i] = new Coordonnee(
                    centreX + (rayon * Math.Cos(angle)), centreY + (rayon * Math.Sin(angle)));
            }

            return points;
        }
    }
}
