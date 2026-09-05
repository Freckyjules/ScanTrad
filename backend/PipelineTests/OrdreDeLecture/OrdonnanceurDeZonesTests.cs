using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.OrdreDeLecture;

namespace ScanTrad.PipelineTests.OrdreDeLecture
{
    /// <summary>
    /// Vérifie l'ordre que rend la règle globale : de haut en bas, puis dans le sens
    /// de lecture à hauteur égale.
    /// </summary>
    /// <remarks>
    /// Les mises en page sont écrites à la main, en rectangles : le test ne dépend
    /// d'aucune image ni d'aucun modèle.
    /// <para>
    /// Un des tests décrit une mise en page que la règle ordonne <em>mal</em>, et
    /// fige ce mauvais ordre exprès. C'est une limite acceptée, pas un bogue en
    /// attente : si quelqu'un réintroduit un jour la structure des cases, ce test
    /// tombera et forcera une décision consciente plutôt qu'un changement discret.
    /// </para>
    /// </remarks>
    public class OrdonnanceurDeZonesTests
    {
        /// <summary>
        /// Deux blocs empilés se lisent de haut en bas, quel que soit le sens.
        /// </summary>
        [Fact]
        public void Ordonner_DeuxBlocsEmpiles_SeLisentDeHautEnBas()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
            {
                Bloc("bas", 100, 900, 300, 100),
                Bloc("haut", 100, 100, 300, 100)
            });

            Assert.Equal(new[] { "haut", "bas" }, Textes(ordre));
        }

        /// <summary>
        /// Deux blocs à la même hauteur se lisent en commençant par la droite, comme
        /// dans un manga d'origine.
        /// </summary>
        [Fact]
        public void Ordonner_DeuxBlocsALaMemeHauteur_CommencentParLaDroite()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
            {
                Bloc("gauche", 100, 100, 300, 200),
                Bloc("droite", 900, 100, 300, 200)
            });

            Assert.Equal(new[] { "droite", "gauche" }, Textes(ordre));
        }

        /// <summary>
        /// Sur une édition retournée, les mêmes blocs se lisent dans l'autre sens.
        /// C'est le seul endroit du calcul que le sens de lecture change.
        /// </summary>
        [Fact]
        public void Ordonner_DeuxBlocsALaMemeHauteur_SensOccidental_CommencentParLaGauche()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(SensDeLecture.GaucheADroite, new[]
            {
                Bloc("gauche", 100, 100, 300, 200),
                Bloc("droite", 900, 100, 300, 200)
            });

            Assert.Equal(new[] { "gauche", "droite" }, Textes(ordre));
        }

        /// <summary>
        /// Deux blocs <em>presque</em> à la même hauteur forment quand même une bande,
        /// et c'est le côté qui les départage.
        /// </summary>
        /// <remarks>
        /// C'est le cas que rate une comparaison stricte des hauteurs. Ici le bloc de
        /// gauche est 18 pixels plus haut que celui de droite : une comparaison au
        /// pixel près trancherait sur cet écart et rendrait « gauche, droite » sur un
        /// manga d'origine, sans jamais regarder le côté. Sur une vraie planche deux
        /// bulles voisines ne sont jamais exactement à la même hauteur, et le sens de
        /// lecture ne servirait donc jamais.
        /// <para>
        /// Les deux boîtes faisant 200 pixels de haut, cet écart les laisse largement
        /// dans la même bande.
        /// </para>
        /// </remarks>
        [Fact]
        public void Ordonner_DeuxBlocsPresqueALaMemeHauteur_CommencentParLaDroite()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
            {
                Bloc("gauche", 100, 100, 300, 200),
                Bloc("droite", 900, 118, 300, 200)
            });

            Assert.Equal(new[] { "droite", "gauche" }, Textes(ordre));
        }

        /// <summary>
        /// La hauteur prime sur le côté : un bloc plus haut passe avant, même s'il se
        /// trouve du côté par lequel on finit de lire.
        /// </summary>
        [Fact]
        public void Ordonner_UnBlocPlusHaut_PasseAvantUnBlocPlusBas()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
            {
                Bloc("plus bas, a droite", 900, 620, 300, 100),
                Bloc("plus haut, a gauche", 100, 400, 300, 100)
            });

            Assert.Equal(
                new[] { "plus haut, a gauche", "plus bas, a droite" },
                Textes(ordre));
        }

        /// <summary>
        /// La limite assumée : deux colonnes de cases s'entrelacent au lieu de
        /// s'enchaîner.
        /// </summary>
        /// <remarks>
        /// Un lecteur descend la colonne de droite en entier avant de passer à celle
        /// de gauche, et attendrait donc « droite haut, droite bas, gauche haut,
        /// gauche bas ». La règle globale alterne, parce qu'elle ne voit que des
        /// hauteurs et ignore qu'il y a deux colonnes.
        /// <para>
        /// Retrouver le bon ordre demanderait de détecter les cases. C'est ce qu'on a
        /// choisi de ne pas faire : le rang reste une proposition, corrigeable depuis
        /// le front.
        /// </para>
        /// </remarks>
        [Fact]
        public void Ordonner_DeuxColonnesDeCases_LesEntrelaceAuLieuDeLesEnchainer()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
            {
                Bloc("droite haut", 900, 100, 300, 100),
                Bloc("gauche haut", 100, 200, 300, 100),
                Bloc("droite bas", 900, 400, 300, 100),
                Bloc("gauche bas", 100, 500, 300, 100)
            });

            Assert.Equal(
                new[] { "droite haut", "gauche haut", "droite bas", "gauche bas" },
                Textes(ordre));
        }

        /// <summary>
        /// Une planche à quatre bandes, dont deux coupées en deux cases : la règle
        /// tombe juste tant que les bandes ne se chevauchent pas verticalement.
        /// </summary>
        [Fact]
        public void Ordonner_PlancheAQuatreBandes_DescendPuisSuitLeSens()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
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
        /// Trois blocs serrés se lisent de haut en bas puis de droite à gauche.
        /// </summary>
        [Fact]
        public void Ordonner_TroisBlocsSerres_SeLisentDeHautEnBasPuisDeDroiteAGauche()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
            {
                Bloc("gauche", 100, 100, 400, 300),
                Bloc("droite", 420, 100, 400, 300),
                Bloc("dessous", 200, 300, 400, 300)
            });

            // Les deux premiers sont exactement à la même hauteur : c'est le sens de
            // lecture qui les départage.
            Assert.Equal(new[] { "droite", "gauche", "dessous" }, Textes(ordre));
        }

        /// <summary>
        /// Le rang est renseigné sur chaque zone, à partir de zéro.
        /// </summary>
        [Fact]
        public void Ordonner_RenseigneLeRangDeChaqueZone()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
            {
                Bloc("bas", 100, 900, 300, 100),
                Bloc("haut", 100, 100, 300, 100)
            });

            Assert.Equal(0, ordre[0].OrdreDeLecture);
            Assert.Equal(1, ordre[1].OrdreDeLecture);
        }

        /// <summary>
        /// Un seul bloc porte le rang zéro.
        /// </summary>
        [Fact]
        public void Ordonner_UneSeuleZone_PorteLeRangZero()
        {
            IReadOnlyList<ZoneDeTexte> ordre = Ordonner(new[]
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
            Assert.Empty(Ordonner());
        }

        /// <summary>
        /// Ordonner rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void Ordonner_Null_LeveArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new OrdonnanceurDeZones().Ordonner(null!));
        }

        private static IReadOnlyList<ZoneDeTexte> Ordonner(params ZoneDeTexte[] zones)
        {
            return Ordonner(SensDeLecture.DroiteAGauche, zones);
        }

        private static IReadOnlyList<ZoneDeTexte> Ordonner(SensDeLecture sens, params ZoneDeTexte[] zones)
        {
            IOrdonnanceurDeZones ordonnanceur = new OrdonnanceurDeZones();

            // L'image ne sert pas à l'ordonnancement ; le sens, lui, vient de la planche.
            return ordonnanceur.Ordonner(new Planche(Array.Empty<byte>(), sens, zones)).Zones;
        }

        private static string[] Textes(IReadOnlyList<ZoneDeTexte> zones)
        {
            return zones.Select(zone => zone.TexteOriginal).ToArray();
        }

        private static ZoneDeTexte Bloc(string texte, double x, double y, double largeur, double hauteur)
        {
            ZoneDeTexte zone = new ZoneDeTexte();

            zone.Rectangle = Quadrilatere.DepuisRectangle(x, y, largeur, hauteur);
            zone.TexteOriginal = texte;
            zone.Confiance = 1;

            return zone;
        }
    }
}
