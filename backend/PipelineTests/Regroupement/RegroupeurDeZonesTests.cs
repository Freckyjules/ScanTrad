using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using ScanTrad.Pipeline.Regroupement;

namespace ScanTrad.PipelineTests.Regroupement
{
    /// <summary>
    /// Vérifie le rassemblement des lignes en blocs.
    /// </summary>
    /// <remarks>
    /// Les coordonnées sont reprises d'une planche réelle mais écrites en dur : le
    /// test ne dépend d'aucune image, d'aucun modèle et d'aucun lecteur. Il tourne en
    /// quelques millisecondes et son résultat est entièrement prévisible.
    /// </remarks>
    public class RegroupeurDeZonesTests
    {
        /// <summary>
        /// Deux lignes de la même bulle ne forment qu'un bloc, et leurs textes se
        /// suivent séparés d'une espace.
        /// </summary>
        [Fact]
        public void Regrouper_DeuxLignesDeLaMemeBulle_NeFontQuUnBloc()
        {
            Bulle bulle = ConstruireBulle(200, 1000, 240, 280);

            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, bulle),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, bulle)
            });

            Assert.Equal("YOU CAN'T", Assert.Single(zones).TexteOriginal);
        }

        /// <summary>
        /// Le regroupement se fait sur la géométrie, pas sur l'identité des objets.
        /// C'est indispensable : le lecteur comic-text-detector fabrique une bulle
        /// distincte pour chaque ligne, même quand c'est la même bulle sur la planche.
        /// </summary>
        [Fact]
        public void Regrouper_MemeBulleMaisInstancesDistinctes_NeFontQuUnBloc()
        {
            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, ConstruireBulle(200, 1000, 240, 280)),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, ConstruireBulle(200, 1000, 240, 280))
            });

            Assert.Equal("YOU CAN'T", Assert.Single(zones).TexteOriginal);
        }

        /// <summary>
        /// Deux bulles distinctes donnent deux blocs distincts.
        /// </summary>
        [Fact]
        public void Regrouper_BullesDifferentes_RestentSeparees()
        {
            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, ConstruireBulle(200, 1000, 240, 280)),
                ConstruireLigne("AHHH!", 940, 1101, 170, 51, ConstruireBulle(920, 1080, 210, 170))
            });

            Assert.Equal(2, zones.Count);
        }

        /// <summary>
        /// Une page est mixte, et c'est la raison d'être de cette conception : chaque
        /// zone est traitée selon ce dont elle dispose, sans que l'appelant ait à
        /// choisir une stratégie pour toute la page.
        /// </summary>
        [Fact]
        public void Regrouper_PageMixte_TraiteChaqueZoneSelonCeQuElleA()
        {
            Bulle premiere = ConstruireBulle(200, 1000, 240, 280);
            Bulle seconde = ConstruireBulle(920, 1080, 210, 170);

            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, premiere),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, premiere),
                ConstruireLigne("AHHH!", 940, 1101, 170, 51, seconde),
                ConstruireLigne("BEAM", 143, 330, 267, 92, null),
                ConstruireLigne("JOLT", 552, 892, 136, 91, null)
            });

            // Deux blocs de bulle, plus les deux onomatopées restées seules.
            Assert.Equal(4, zones.Count);
            Assert.Contains(zones, zone => zone.TexteOriginal == "YOU CAN'T");
            Assert.Contains(zones, zone => zone.TexteOriginal == "AHHH!");
            Assert.Contains(zones, zone => zone.TexteOriginal == "BEAM");
            Assert.Contains(zones, zone => zone.TexteOriginal == "JOLT");
        }

        /// <summary>
        /// Une onomatopée dessinée à même la planche n'a pas de bulle : elle ne peut
        /// être rattachée à rien et ressort seule, sa bulle toujours vide.
        /// </summary>
        [Fact]
        public void Regrouper_ZoneSansBulle_RessortSeule()
        {
            Bulle bulle = ConstruireBulle(200, 1000, 240, 280);

            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, bulle),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, bulle),
                ConstruireLigne("BEAM", 143, 330, 267, 92, null)
            });

            Assert.Equal(2, zones.Count);
            Assert.Contains(zones, zone => zone.TexteOriginal == "BEAM" && zone.Bulle == null);
        }

        /// <summary>
        /// Deux zones sans bulle, éloignées, ne se rejoignent pas.
        /// </summary>
        [Fact]
        public void Regrouper_ZonesSansBulleEloignees_RestentSeparees()
        {
            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("BEAM", 143, 330, 267, 92, null),
                ConstruireLigne("JOLT", 552, 892, 136, 91, null)
            });

            Assert.Equal(2, zones.Count);
        }

        /// <summary>
        /// Deux zones sans bulle à la même hauteur mais sans recouvrement horizontal
        /// ne se rejoignent pas non plus : elles ne sont pas dans le même cartouche.
        /// </summary>
        [Fact]
        public void Regrouper_ZonesSansBulleCoteACote_RestentSeparees()
        {
            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, null),
                ConstruireLigne("AHHH!", 940, 1017, 170, 49, null)
            });

            Assert.Equal(2, zones.Count);
        }

        /// <summary>
        /// Deux lignes empilées sans bulle appartiennent à un même cartouche sans
        /// contour et devraient se rejoindre. Le repli géométrique n'est pas encore
        /// écrit : aujourd'hui chaque zone sans bulle ressort seule.
        /// </summary>
        [Fact(Skip = "Le repli par proximité reste à écrire.")]
        public void Regrouper_ZonesSansBulleEmpilees_NeFontQuUnBloc()
        {
            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, null),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, null)
            });

            Assert.Equal("YOU CAN'T", Assert.Single(zones).TexteOriginal);
        }

        /// <summary>
        /// Les textes d'un bloc se suivent de haut en bas, quel que soit l'ordre dans
        /// lequel le lecteur les a rendus.
        /// </summary>
        [Fact]
        public void Regrouper_LignesDansLeDesordre_LesRemetDeHautEnBas()
        {
            Bulle bulle = ConstruireBulle(200, 1000, 240, 280);

            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("GO OFF", 236, 1099, 153, 44, bulle),
                ConstruireLigne("YOU", 260, 1017, 100, 49, bulle),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, bulle)
            });

            Assert.Equal("YOU CAN'T GO OFF", Assert.Single(zones).TexteOriginal);
        }

        /// <summary>
        /// Un bloc ne vaut que ce que vaut sa ligne la moins sûre.
        /// </summary>
        [Fact]
        public void Regrouper_Confiance_PrendLaPlusBasseDuBloc()
        {
            Bulle bulle = ConstruireBulle(200, 1000, 240, 280);

            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, bulle, 0.99),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, bulle, 0.62)
            });

            Assert.Equal(0.62, Assert.Single(zones).Confiance, 10);
        }

        /// <summary>
        /// Le quadrilatère du bloc couvre toutes ses lignes.
        /// </summary>
        [Fact]
        public void Regrouper_Quadrilatere_EnglobeToutesLesLignes()
        {
            Bulle bulle = ConstruireBulle(200, 1000, 240, 280);

            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, bulle),
                ConstruireLigne("GO OFF", 236, 1099, 153, 44, bulle)
            });

            Quadrilatere bloc = Assert.Single(zones).Quadrilatere;

            Assert.True(bloc.HautGauche.X <= 236, "Le bloc doit atteindre la ligne la plus à gauche.");
            Assert.True(bloc.HautGauche.Y <= 1017, "Le bloc doit atteindre la ligne la plus haute.");
            Assert.True(bloc.BasDroit.X >= 389, "Le bloc doit atteindre la ligne la plus à droite.");
            Assert.True(bloc.BasDroit.Y >= 1143, "Le bloc doit atteindre la ligne la plus basse.");
        }

        /// <summary>
        /// Le bloc garde la bulle de ses lignes : c'est elle qui servira à effacer et
        /// à faire tenir le texte français.
        /// </summary>
        [Fact]
        public void Regrouper_Bloc_ConserveLaBulle()
        {
            Bulle bulle = ConstruireBulle(200, 1000, 240, 280);

            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, bulle),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, bulle)
            });

            Assert.NotNull(Assert.Single(zones).Bulle);
        }

        /// <summary>
        /// Le regroupement ne traduit ni ne trie : ces champs restent vides.
        /// </summary>
        [Fact]
        public void Regrouper_NeRemplitNiTraductionNiOrdreDeLecture()
        {
            Bulle bulle = ConstruireBulle(200, 1000, 240, 280);

            IReadOnlyList<ZoneDeTexte> zones = Regroupeur().Regrouper(new[]
            {
                ConstruireLigne("YOU", 260, 1017, 100, 49, bulle),
                ConstruireLigne("CAN'T", 252, 1058, 121, 45, bulle)
            });

            ZoneDeTexte bloc = Assert.Single(zones);

            Assert.Null(bloc.TexteTraduit);
            Assert.Null(bloc.OrdreDeLecture);
        }

        /// <summary>
        /// Une page sans texte ne pose pas de problème.
        /// </summary>
        [Fact]
        public void Regrouper_ListeVide_RendUneListeVide()
        {
            Assert.Empty(Regroupeur().Regrouper(Array.Empty<ZoneDeTexte>()));
        }

        /// <summary>
        /// Regrouper rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public void Regrouper_Null_LeveArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => Regroupeur().Regrouper(null!));
        }

        private static IRegroupeurDeZones Regroupeur()
        {
            return new RegroupeurDeZones();
        }

        private static ZoneDeTexte ConstruireLigne(
            string texte,
            double x,
            double y,
            double largeur,
            double hauteur,
            Bulle? bulle,
            double confiance = 0.9)
        {
            ZoneDeTexte zone = new ZoneDeTexte();

            zone.Quadrilatere = Quadrilatere.DepuisRectangle(x, y, largeur, hauteur);
            zone.TexteOriginal = texte;
            zone.Confiance = confiance;
            zone.Bulle = bulle;

            return zone;
        }

        private static Bulle ConstruireBulle(double x, double y, double largeur, double hauteur)
        {
            return new Bulle(new List<Coordonnee>
            {
                new Coordonnee(x, y),
                new Coordonnee(x + largeur, y),
                new Coordonnee(x + largeur, y + hauteur),
                new Coordonnee(x, y + hauteur)
            });
        }
    }
}
