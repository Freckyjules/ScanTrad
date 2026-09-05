using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Lecture;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests.Lecture
{
    /// <summary>
    /// Lit la planche d'essai une seule fois et partage le résultat entre les tests
    /// de la classe.
    /// </summary>
    /// <remarks>
    /// Charger le modèle et lire une planche prend une vingtaine de secondes. Sans ce
    /// partage, chaque test refait ce travail sur exactement la même image.
    /// </remarks>
    public class LectureDeLaPlancheDEssai : IAsyncLifetime
    {
        #region Attributs

        private Planche originale;
        private Planche lue;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise le partage. La lecture n'a lieu qu'au moment où xUnit appelle
        /// <see cref="InitializeAsync"/>.
        /// </summary>
        public LectureDeLaPlancheDEssai()
        {
            originale = new Planche(Array.Empty<byte>());
            lue = originale;
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// La planche telle qu'elle est arrivée, avant toute lecture.
        /// </summary>
        public Planche Originale
        {
            get { return originale; }
        }

        /// <summary>
        /// La planche après lecture, avec ses zones.
        /// </summary>
        public Planche Lue
        {
            get { return lue; }
        }

        /// <summary>
        /// Les zones trouvées sur la planche d'essai.
        /// </summary>
        public IReadOnlyList<ZoneDeTexte> Zones
        {
            get { return lue.Zones; }
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Charge la planche et la lit, une fois pour toutes.
        /// </summary>
        /// <returns>La tâche de la lecture.</returns>
        public async ValueTask InitializeAsync()
        {
            originale = new Planche(await PlancheDEssai.ChargerAsync());

            using ILecteurDePlanche lecteur =
                new LecteurDePlancheComicTextDetector(PlancheDEssai.TrouverLeModele());

            lue = await lecteur.LireAsync(originale);
        }

        /// <summary>
        /// Rien à libérer : le lecteur l'a déjà été à la fin de la lecture.
        /// </summary>
        /// <returns>Une tâche déjà terminée.</returns>
        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            return ValueTask.CompletedTask;
        }

        #endregion
    }
}
