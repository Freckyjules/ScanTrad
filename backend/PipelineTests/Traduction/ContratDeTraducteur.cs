using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Traduction
{
    /// <summary>
    /// Les règles que <b>tout</b> traducteur doit respecter, quel que soit son moteur.
    /// </summary>
    /// <remarks>
    /// Une classe de test par implémentation hérite d'ici et fournit le traducteur à
    /// éprouver. Un nouveau moteur ne peut donc pas arriver en oubliant discrètement
    /// une règle du contrat : il hérite des vérifications avec le reste.
    /// <para>
    /// Rien ici ne regarde <em>ce que</em> le moteur a traduit. Figer une traduction
    /// reviendrait à figer le comportement d'un modèle qu'on ne maîtrise pas, et
    /// interdirait de comparer deux moteurs avec les mêmes tests. On vérifie ce que le
    /// contrat promet : ce qui doit être rempli, et surtout ce qui ne doit pas bouger.
    /// </para>
    /// </remarks>
    public abstract class ContratDeTraducteur
    {
        /// <summary>
        /// Construit le traducteur à éprouver.
        /// </summary>
        /// <returns>Un traducteur neuf, dont le test devient responsable.</returns>
        protected abstract ITraducteur Creer();

        /// <summary>
        /// Traduire rien n'a pas de sens et doit être signalé.
        /// </summary>
        [Fact]
        public async Task Traduire_Null_LeveArgumentNullException()
        {
            using ITraducteur traducteur = Creer();

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => traducteur.TraduireAsync(null!, TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// Les zones, leur géométrie et le sens de lecture traversent l'étape intacts.
        /// </summary>
        [Fact]
        public async Task Traduire_ConserveLesZonesLaGeometrieEtLeSens()
        {
            ZoneDeTexte zone = Zone("HELLO");
            Planche recue = PlancheAvec(zone);

            using ITraducteur traducteur = Creer();

            Planche traduite = await traducteur.TraduireAsync(recue, TestContext.Current.CancellationToken);

            Assert.Equal(SensDeLecture.GaucheADroite, traduite.Sens);

            ZoneDeTexte apres = Assert.Single(traduite.Zones);

            Assert.Equal(zone.Rectangle.HautGauche.X, apres.Rectangle.HautGauche.X);
            Assert.Equal(zone.Rectangle.Largeur, apres.Rectangle.Largeur);
            Assert.Equal(zone.Confiance, apres.Confiance);
        }

        /// <summary>
        /// Ni le texte d'origine ni le rang de lecture ne bougent : la traduction ne
        /// remplit qu'un seul champ.
        /// </summary>
        [Fact]
        public async Task Traduire_NeToucheNiAuTexteOriginalNiAuRang()
        {
            ZoneDeTexte zone = Zone("HELLO");
            zone.OrdreDeLecture = 3;

            using ITraducteur traducteur = Creer();

            Planche traduite = await traducteur.TraduireAsync(PlancheAvec(zone), TestContext.Current.CancellationToken);

            ZoneDeTexte apres = Assert.Single(traduite.Zones);

            Assert.Equal("HELLO", apres.TexteOriginal);
            Assert.Equal(3, apres.OrdreDeLecture);
        }

        /// <summary>
        /// Une planche sans zone traverse l'étape sans rien demander au moteur.
        /// </summary>
        [Fact]
        public async Task Traduire_PlancheSansZone_RendUnePlancheSansZone()
        {
            using ITraducteur traducteur = Creer();

            Planche traduite = await traducteur.TraduireAsync(PlancheAvec(), TestContext.Current.CancellationToken);

            Assert.Empty(traduite.Zones);
        }

        /// <summary>
        /// Construit une planche portant les zones indiquées. L'image ne sert pas à la
        /// traduction.
        /// </summary>
        /// <param name="zones">Les zones de la planche.</param>
        /// <returns>Une planche sans image, en lecture occidentale.</returns>
        protected static Planche PlancheAvec(params ZoneDeTexte[] zones)
        {
            return new Planche(Array.Empty<byte>(), SensDeLecture.GaucheADroite, zones);
        }

        /// <summary>
        /// Construit une zone portant le texte indiqué, avec une géométrie plausible.
        /// </summary>
        /// <param name="texte">Le texte d'origine de la zone.</param>
        /// <returns>Une zone prête à être traduite.</returns>
        protected static ZoneDeTexte Zone(string texte)
        {
            ZoneDeTexte zone = new ZoneDeTexte();

            zone.Rectangle = Quadrilatere.DepuisRectangle(100, 200, 300, 120);
            zone.TexteOriginal = texte;
            zone.Confiance = 0.9;

            return zone;
        }
    }
}
