using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Models
{
    /// <summary>
    /// Vérifie le contour d'une bulle : sa validité, son centre, et le test
    /// d'appartenance d'un point.
    /// </summary>
    public class BulleTests
    {
        /// <summary>
        /// Un point au milieu d'un contour carré est bien vu à l'intérieur.
        /// </summary>
        [Fact]
        public void Contient_PointAuCentreDunCarre_RetourneVrai()
        {
            Bulle bulle = ConstruireCarre();

            Assert.True(bulle.Contient(new Coordonnee(5, 5)));
        }

        /// <summary>
        /// Un point situé au-delà du contour est vu à l'extérieur.
        /// </summary>
        [Fact]
        public void Contient_PointHorsDuCarre_RetourneFaux()
        {
            Bulle bulle = ConstruireCarre();

            Assert.False(bulle.Contient(new Coordonnee(50, 5)));
        }

        /// <summary>
        /// Sur une forme concave, un point logé dans le creux est bien vu dehors.
        /// C'est le cas que ne saurait pas traiter un simple test de rectangle
        /// englobant, et il se présente sur les bulles de cri et les bulles à queue.
        /// </summary>
        [Fact]
        public void Contient_FormeConcave_PointDansLeCreux_RetourneFaux()
        {
            Bulle bulle = ConstruireFormeEnL();

            Assert.False(bulle.Contient(new Coordonnee(7, 7)));
        }

        /// <summary>
        /// Sur la même forme concave, les deux branches du L contiennent bien
        /// leurs points.
        /// </summary>
        [Fact]
        public void Contient_FormeConcave_PointsDansLesBranches_RetourneVrai()
        {
            Bulle bulle = ConstruireFormeEnL();

            Assert.True(bulle.Contient(new Coordonnee(7, 2)));
            Assert.True(bulle.Contient(new Coordonnee(2, 7)));
        }

        /// <summary>
        /// Une bulle sans contour ne contient rien, et ne doit pas échouer.
        /// </summary>
        [Fact]
        public void Contient_ContourVide_RetourneFaux()
        {
            Bulle bulle = new Bulle();

            Assert.False(bulle.Contient(new Coordonnee(0, 0)));
        }

        /// <summary>
        /// Situer un point inexistant n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void Contient_Null_LeveArgumentNullException()
        {
            Bulle bulle = ConstruireCarre();

            Assert.Throws<ArgumentNullException>(() => bulle.Contient(null!));
        }

        /// <summary>
        /// Le centre d'un contour carré est son milieu.
        /// </summary>
        [Fact]
        public void Centre_Carre_RetourneLeMilieu()
        {
            Bulle bulle = ConstruireCarre();

            Assert.Equal(5, bulle.Centre.X, 10);
            Assert.Equal(5, bulle.Centre.Y, 10);
        }

        /// <summary>
        /// Une bulle encore vide ramène son centre à l'origine plutôt que d'échouer.
        /// </summary>
        [Fact]
        public void Centre_ContourVide_RetourneLOrigine()
        {
            Bulle bulle = new Bulle();

            Assert.Equal(0, bulle.Centre.X, 10);
            Assert.Equal(0, bulle.Centre.Y, 10);
        }

        /// <summary>
        /// Deux points ne dessinent pas un polygone.
        /// </summary>
        [Fact]
        public void Constructeur_MoinsDeTroisPoints_LeveArgumentException()
        {
            List<Coordonnee> deuxPoints = new List<Coordonnee>
            {
                new Coordonnee(0, 0),
                new Coordonnee(10, 0)
            };

            Assert.Throws<ArgumentException>(() => new Bulle(deuxPoints));
        }

        /// <summary>
        /// Construire une bulle à partir de rien doit être signalé.
        /// </summary>
        [Fact]
        public void Constructeur_Null_LeveArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Bulle(null!));
        }

        private static Bulle ConstruireCarre()
        {
            return new Bulle(new List<Coordonnee>
            {
                new Coordonnee(0, 0),
                new Coordonnee(10, 0),
                new Coordonnee(10, 10),
                new Coordonnee(0, 10)
            });
        }

        private static Bulle ConstruireFormeEnL()
        {
            return new Bulle(new List<Coordonnee>
            {
                new Coordonnee(0, 0),
                new Coordonnee(10, 0),
                new Coordonnee(10, 4),
                new Coordonnee(4, 4),
                new Coordonnee(4, 10),
                new Coordonnee(0, 10)
            });
        }
    }
}
