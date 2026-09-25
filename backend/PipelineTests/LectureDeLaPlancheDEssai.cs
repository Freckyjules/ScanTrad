using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Lecture;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests
{
    /// <summary>
    /// Lit la planche d'essai une seule fois et partage le résultat entre tous les
    /// tests d'intégration.
    /// </summary>
    /// <remarks>
    /// Charger le modèle et lire une planche prend une vingtaine de secondes. Le
    /// partage est déclaré sur <see cref="CollectionDIntegration"/> et non sur une
    /// classe : sans ça, chaque classe de test refait ce travail sur exactement la
    /// même image.
    /// <para>
    /// Les tests qui font travailler une étape suivante ne doivent pas toucher aux
    /// zones partagées : l'ordonnanceur écrit le rang <em>dans</em> les zones qu'on
    /// lui donne, et un test qui les lui passerait directement casserait celui qui
    /// vérifie que la lecture ne renseigne pas l'ordre. Ils demandent donc une
    /// <see cref="Copier"/> — la lecture reste faite une fois, seuls les quelques
    /// objets qu'elle a produits sont recopiés.
    /// </para>
    /// </remarks>
    public class LectureDeLaPlancheDEssai : IAsyncLifetime
    {
        #region Attributs

        private Planche originale;
        private Planche lue;

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
        /// La planche après lecture, avec ses zones. À ne consulter qu'en lecture :
        /// tout test qui modifie ses zones perturbe les autres.
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

        #region Méthodes

        /// <summary>
        /// Charge la planche et la lit, une fois pour toutes.
        /// </summary>
        /// <returns>La tâche de la lecture.</returns>
        public async ValueTask InitializeAsync()
        {
            originale = new Planche(await PlancheDEssai.ChargerAsync());

            using ILecteurDePlanche lecteur =
                new LecteurDePlanche(PlancheDEssai.TrouverLeModele());

            lue = await lecteur.LireAsync(originale);
        }

        /// <summary>
        /// Rend une planche indépendante portant le même résultat de lecture, dans le
        /// sens demandé. C'est ce qu'utilise un test qui fait travailler une étape
        /// suivante, pour que ce qu'elle écrit ne sorte pas de ce test.
        /// </summary>
        /// <param name="sens">Le sens de lecture de la planche voulue.</param>
        /// <returns>
        /// Une planche portant la même image et des zones recopiées, qu'on peut
        /// modifier librement.
        /// </returns>
        public Planche Copier(SensDeLecture sens)
        {
            return new Planche(lue.Image, sens, lue.Zones.Select(zone => Copier(zone)));
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

        #region Méthodes privées

        private static ZoneDeTexte Copier(ZoneDeTexte zone)
        {
            ZoneDeTexte copie = new ZoneDeTexte();

            copie.Rectangle = Copier(zone.Rectangle);
            copie.Angle = zone.Angle;
            copie.HauteurDeLigne = zone.HauteurDeLigne;
            copie.Bulle = Copier(zone.Bulle);
            copie.CouleurDeFond = Copier(zone.CouleurDeFond);
            copie.TexteOriginal = zone.TexteOriginal;
            copie.TexteTraduit = zone.TexteTraduit;
            copie.Confiance = zone.Confiance;
            copie.OrdreDeLecture = zone.OrdreDeLecture;

            return copie;
        }

        private static Bulle? Copier(Bulle? bulle)
        {
            if (bulle == null)
            {
                return null;
            }

            return new Bulle(bulle.Contour.Select(point => Copier(point)));
        }

        private static Couleur? Copier(Couleur? couleur)
        {
            if (couleur == null)
            {
                return null;
            }

            return new Couleur(couleur.Rouge, couleur.Vert, couleur.Bleu);
        }

        private static Quadrilatere Copier(Quadrilatere quadrilatere)
        {
            return new Quadrilatere(
                Copier(quadrilatere.HautGauche),
                Copier(quadrilatere.HautDroit),
                Copier(quadrilatere.BasDroit),
                Copier(quadrilatere.BasGauche));
        }

        private static Coordonnee Copier(Coordonnee point)
        {
            return new Coordonnee(point.X, point.Y);
        }

        #endregion
    }
}
