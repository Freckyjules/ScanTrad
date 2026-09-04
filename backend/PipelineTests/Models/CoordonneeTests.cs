using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Models
{
    /// <summary>
    /// Vérifie le calcul de distance entre deux coordonnées.
    /// </summary>
    public class CoordonneeTests
    {
        /// <summary>
        /// Sur un triangle 3-4-5, la distance vaut exactement 5.
        /// </summary>
        [Fact]
        public void DistanceVers_TriangleTroisQuatreCinq_RetourneCinq()
        {
            Coordonnee origine = new Coordonnee(0, 0);
            Coordonnee cible = new Coordonnee(3, 4);

            double distance = origine.DistanceVers(cible);

            Assert.Equal(5, distance, 10);
        }

        /// <summary>
        /// La distance d'un point à lui-même est nulle.
        /// </summary>
        [Fact]
        public void DistanceVers_MemePosition_RetourneZero()
        {
            Coordonnee point = new Coordonnee(42, 17);
            Coordonnee jumeau = new Coordonnee(42, 17);

            double distance = point.DistanceVers(jumeau);

            Assert.Equal(0, distance, 10);
        }

        /// <summary>
        /// La distance ne dépend pas du sens dans lequel on la mesure.
        /// </summary>
        [Fact]
        public void DistanceVers_SensInverse_RetourneLaMemeDistance()
        {
            Coordonnee premier = new Coordonnee(-5, 8);
            Coordonnee second = new Coordonnee(11, -2);

            Assert.Equal(premier.DistanceVers(second), second.DistanceVers(premier), 10);
        }

        /// <summary>
        /// Mesurer vers rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void DistanceVers_Null_LeveArgumentNullException()
        {
            Coordonnee point = new Coordonnee(0, 0);

            Assert.Throws<ArgumentNullException>(() => point.DistanceVers(null!));
        }
    }
}
