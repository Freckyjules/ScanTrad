using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.OrdreDeLecture;

namespace ScanTrad.PipelineTests.OrdreDeLecture
{
    /// <summary>
    /// Vérifie que la coupe récursive retrouve l'ordre de lecture d'une planche.
    /// </summary>
    /// <remarks>
    /// Les mises en page sont écrites à la main, en rectangles : le test ne dépend
    /// d'aucune image ni d'aucun modèle. Elles reprennent la structure de planches
    /// réelles — bandes empilées, cases côte à côte, cases imbriquées.
    /// </remarks>
    public class OrdonnanceurParCoupeRecursiveTests
    {
        /// <summary>
        /// Deux bandes empilées se lisent de haut en bas, quel que soit le sens.
        /// </summary>
        [Fact]
        public void Ordonner_DeuxBandesEmpilees_SeLisentDeHautEnBas()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonnanceur().Ordonner(new[]
            {
                Bloc("bas", 100, 900, 300, 100),
                Bloc("haut", 100, 100, 300, 100)
            });

            Assert.Equal(new[] { "haut", "bas" }, Textes(ordre));
        }

        /// <summary>
        /// Deux cases côte à côte se lisent en commençant par la droite, comme dans
        /// un manga d'origine.
        /// </summary>
        [Fact]
        public void Ordonner_DeuxCasesCoteACote_CommencentParLaDroite()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonnanceur().Ordonner(new[]
            {
                Bloc("gauche", 100, 100, 300, 200),
                Bloc("droite", 900, 100, 300, 200)
            });

            Assert.Equal(new[] { "droite", "gauche" }, Textes(ordre));
        }

        /// <summary>
        /// Sur une édition retournée, les mêmes cases se lisent dans l'autre sens.
        /// C'est le seul endroit du calcul que le sens de lecture change.
        /// </summary>
        [Fact]
        public void Ordonner_DeuxCasesCoteACote_SensOccidental_CommencentParLaGauche()
        {
            IReadOnlyList<ZoneDeTexte> ordre =
                new OrdonnanceurParCoupeRecursive(SensDeLecture.GaucheADroite).Ordonner(new[]
                {
                    Bloc("gauche", 100, 100, 300, 200),
                    Bloc("droite", 900, 100, 300, 200)
                });

            Assert.Equal(new[] { "gauche", "droite" }, Textes(ordre));
        }

        /// <summary>
        /// Le cas qu'aucun tri ne saurait traiter : une bulle plus basse et plus à
        /// gauche passe avant une bulle plus haute et plus à droite, parce qu'elle
        /// appartient à la bande du dessus. Deux positions relatives identiques
        /// donnent deux ordres opposés selon la structure autour d'elles.
        /// </summary>
        [Fact]
        public void Ordonner_BulleBasseDansLaBandeDuDessus_PasseAvantUneBulleHauteDeLaBandeSuivante()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonnanceur().Ordonner(new[]
            {
                Bloc("bande 2, en haut a droite", 900, 620, 300, 100),
                Bloc("bande 1, en bas a gauche", 100, 400, 300, 100)
            });

            Assert.Equal(
                new[] { "bande 1, en bas a gauche", "bande 2, en haut a droite" },
                Textes(ordre));
        }

        /// <summary>
        /// Une planche complète : deux bandes pleine largeur, puis une bande coupée
        /// en deux cases. C'est la structure de la planche d'essai.
        /// </summary>
        [Fact]
        public void Ordonner_PlancheAQuatreBandes_SuitLaStructure()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonnanceur().Ordonner(new[]
            {
                Bloc("4 gauche", 100, 2400, 400, 200),
                Bloc("3 droite", 900, 1500, 400, 200),
                Bloc("1", 100, 100, 1200, 300),
                Bloc("4 droite", 900, 2400, 400, 200),
                Bloc("2", 100, 700, 1200, 300),
                Bloc("3 gauche", 100, 1500, 400, 200)
            });

            Assert.Equal(
                new[] { "1", "2", "3 droite", "3 gauche", "4 droite", "4 gauche" },
                Textes(ordre));
        }

        /// <summary>
        /// Dans une même case, plusieurs bulles se lisent de haut en bas puis de
        /// droite à gauche.
        /// </summary>
        [Fact]
        public void Ordonner_TroisBullesDansUneMemeCase_SeLisentDeHautEnBasPuisDeDroiteAGauche()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonnanceur().Ordonner(new[]
            {
                Bloc("gauche", 100, 100, 400, 300),
                Bloc("droite", 420, 100, 400, 300),
                Bloc("dessous", 200, 300, 400, 300)
            });

            // Les trois se chevauchent sur les deux axes : aucune coupe n'est
            // possible. Les deux premières étant à la même hauteur, c'est le sens de
            // lecture qui les départage.
            Assert.Equal(new[] { "droite", "gauche", "dessous" }, Textes(ordre));
        }

        /// <summary>
        /// Le rang est renseigné sur chaque zone, à partir de zéro.
        /// </summary>
        [Fact]
        public void Ordonner_RenseigneLeRangDeChaqueZone()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonnanceur().Ordonner(new[]
            {
                Bloc("bas", 100, 900, 300, 100),
                Bloc("haut", 100, 100, 300, 100)
            });

            Assert.Equal(0, ordre[0].OrdreDeLecture);
            Assert.Equal(1, ordre[1].OrdreDeLecture);
        }

        /// <summary>
        /// Une seule bulle porte le rang zéro.
        /// </summary>
        [Fact]
        public void Ordonner_UneSeuleZone_PorteLeRangZero()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonnanceur().Ordonner(new[]
            {
                Bloc("seule", 100, 100, 300, 100)
            });

            Assert.Equal(0, Assert.Single(ordre).OrdreDeLecture);
        }

        /// <summary>
        /// Une page sans texte ne pose pas de problème.
        /// </summary>
        [Fact]
        public void Ordonner_ListeVide_RendUneListeVide()
        {
            Assert.Empty(Ordonnanceur().Ordonner(Array.Empty<ZoneDeTexte>()));
        }

        /// <summary>
        /// Ordonner rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void Ordonner_Null_LeveArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Ordonnanceur().Ordonner(null!));
        }

        private static IOrdonnanceurDeZones Ordonnanceur()
        {
            return new OrdonnanceurParCoupeRecursive();
        }

        private static string[] Textes(IReadOnlyList<ZoneDeTexte> zones)
        {
            return zones.Select(zone => zone.TexteOriginal).ToArray();
        }

        private static ZoneDeTexte Bloc(string texte, double x, double y, double largeur, double hauteur)
        {
            ZoneDeTexte zone = new ZoneDeTexte();

            zone.Quadrilatere = Quadrilatere.DepuisRectangle(x, y, largeur, hauteur);
            zone.TexteOriginal = texte;
            zone.Confiance = 1;

            return zone;
        }
    }
}
