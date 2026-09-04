using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Models
{
    /// <summary>
    /// Vérifie la géométrie d'un quadrilatère : dimensions, centre et inclinaison.
    /// </summary>
    public class QuadrilatereTests
    {
        /// <summary>
        /// La fabrique place les quatre coins dans le sens horaire depuis le haut gauche.
        /// </summary>
        [Fact]
        public void DepuisRectangle_RectangleSimple_PlaceLesQuatreCoins()
        {
            Quadrilatere quad = Quadrilatere.DepuisRectangle(10, 20, 100, 50);

            Assert.Equal(10, quad.HautGauche.X, 10);
            Assert.Equal(20, quad.HautGauche.Y, 10);
            Assert.Equal(110, quad.HautDroit.X, 10);
            Assert.Equal(20, quad.HautDroit.Y, 10);
            Assert.Equal(110, quad.BasDroit.X, 10);
            Assert.Equal(70, quad.BasDroit.Y, 10);
            Assert.Equal(10, quad.BasGauche.X, 10);
            Assert.Equal(70, quad.BasGauche.Y, 10);
        }

        /// <summary>
        /// Sur un rectangle droit, largeur et hauteur sont celles du rectangle.
        /// </summary>
        [Fact]
        public void LargeurEtHauteur_RectangleDroit_ReprennentLesDimensions()
        {
            Quadrilatere quad = Quadrilatere.DepuisRectangle(0, 0, 100, 40);

            Assert.Equal(100, quad.Largeur, 10);
            Assert.Equal(40, quad.Hauteur, 10);
        }

        /// <summary>
        /// Le centre d'un rectangle droit est son milieu.
        /// </summary>
        [Fact]
        public void Centre_RectangleDroit_RetourneLeMilieu()
        {
            Quadrilatere quad = Quadrilatere.DepuisRectangle(0, 0, 100, 40);

            Assert.Equal(50, quad.Centre.X, 10);
            Assert.Equal(20, quad.Centre.Y, 10);
        }

        /// <summary>
        /// Un texte horizontal n'a aucune inclinaison.
        /// </summary>
        [Fact]
        public void Angle_TexteHorizontal_RetourneZero()
        {
            Quadrilatere quad = Quadrilatere.DepuisRectangle(0, 0, 100, 40);

            Assert.Equal(0, quad.Angle, 10);
        }

        /// <summary>
        /// Un texte qui descend vers la droite a un angle positif, l'axe vertical
        /// de l'image étant orienté vers le bas.
        /// </summary>
        [Fact]
        public void Angle_TexteQuiDescendVersLaDroite_RetourneQuaranteCinqDegres()
        {
            Quadrilatere quad = new Quadrilatere(
                new Coordonnee(0, 0),
                new Coordonnee(10, 10),
                new Coordonnee(5, 15),
                new Coordonnee(-5, 5));

            Assert.Equal(45, quad.Angle, 10);
        }

        /// <summary>
        /// Un texte qui monte vers la droite a un angle négatif.
        /// </summary>
        [Fact]
        public void Angle_TexteQuiMonteVersLaDroite_RetourneAngleNegatif()
        {
            Quadrilatere quad = new Quadrilatere(
                new Coordonnee(0, 10),
                new Coordonnee(10, 0),
                new Coordonnee(15, 5),
                new Coordonnee(5, 15));

            Assert.True(quad.Angle < 0);
        }

        /// <summary>
        /// Un quadrilatère sans l'un de ses coins n'a pas de sens.
        /// </summary>
        [Fact]
        public void Constructeur_CoinNull_LeveArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Quadrilatere(
                new Coordonnee(0, 0),
                null!,
                new Coordonnee(10, 10),
                new Coordonnee(0, 10)));
        }
    }
}
