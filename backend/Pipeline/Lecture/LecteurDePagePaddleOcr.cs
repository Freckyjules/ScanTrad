using OpenCvSharp;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace ScanTrad.Pipeline.Lecture
{
    /// <summary>
    /// Lit une planche avec PaddleOCR, qui tourne entièrement sur la machine locale.
    /// </summary>
    /// <remarks>
    /// Le nom porte la technologie employée, pour qu'une autre implémentation puisse
    /// vivre à côté et être comparée sur les mêmes planches.
    /// <para>
    /// La sortie est brute : une zone par ligne détectée, dans l'ordre où le moteur
    /// les a rencontrées. Ni le regroupement des lignes en bulles, ni l'ordre de
    /// lecture ne sont du ressort de cette classe — ce sont des calculs sur
    /// l'ensemble de la page, qui demandent une connaissance du manga qu'un moteur
    /// d'OCR n'a pas.
    /// </para>
    /// <para>
    /// L'objet retient un moteur PaddleOCR chargé en mémoire. Il coûte cher à
    /// construire — il faut charger les modèles — donc mieux vaut le garder d'une
    /// page à l'autre que d'en créer un par page. Il doit être libéré avec
    /// <see cref="Dispose"/>.
    /// </para>
    /// </remarks>
    public class LecteurDePagePaddleOcr : ILecteurDePage
    {
        #region Attributs

        private PaddleOcrAll moteur;
        private double confianceMinimale;
        private bool libere;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un lecteur avec le modèle anglais et un seuil de confiance courant.
        /// </summary>
        public LecteurDePagePaddleOcr()
            : this(0.5)
        {
        }

        /// <summary>
        /// Initialise un lecteur avec un seuil de confiance choisi.
        /// </summary>
        /// <param name="confianceMinimale">
        /// En dessous de ce seuil, une ligne est jetée sans être remontée. Les moteurs
        /// trouvent régulièrement du « texte » dans une trame de fond ou un motif de
        /// vêtement, et le rendent avec une confiance très basse.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Levée si <paramref name="confianceMinimale"/> sort de l'intervalle 0-1.
        /// </exception>
        public LecteurDePagePaddleOcr(double confianceMinimale)
        {
            if (confianceMinimale < 0 || confianceMinimale > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(confianceMinimale), "Le seuil de confiance se situe entre 0 et 1.");
            }

            this.confianceMinimale = confianceMinimale;
            this.libere = false;

            this.moteur = new PaddleOcrAll(LocalFullModels.EnglishV5)
            {
                // Le détecteur exprime beaucoup de lignes horizontales comme des
                // rectangles pivotés d'un quart de tour. Laisser PaddleOCR redresser
                // les découpes sur cette base lui fait lire du texte couché : sur une
                // planche d'essai, « MAGIC ACADEMY » ressortait en « WAMIC ACADEAA ».
                // Sans redressement, il découpe droit et lit juste.
                AllowRotateDetection = false,

                // Le classifieur d'orientation à 180° fait planter le moteur natif
                // (« OneDnnContext does not have the input Filter »). On s'en passe :
                // une planche de manga n'est pas à l'envers.
                Enable180Classification = false
            };

            // Une planche fait couramment 2000 à 3000 pixels de haut. Sans relever
            // cette borne, le détecteur réduit l'image avant de travailler et le
            // texte des bulles devient trop petit pour être trouvé.
            this.moteur.Detector.MaxSize = 2048;
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Seuil en dessous duquel une ligne lue est jetée, entre 0 et 1.
        /// </summary>
        public double ConfianceMinimale
        {
            get { return confianceMinimale; }
            set { confianceMinimale = value; }
        }

        #endregion

        #region Méthodes

        /// <inheritdoc />
        public Task<IReadOnlyList<ZoneDeTexte>> LireAsync(byte[] image, CancellationToken jetonAnnulation = default)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (image.Length == 0)
            {
                throw new ArgumentException("L'image fournie est vide.", nameof(image));
            }

            ObjectDisposedException.ThrowIf(libere, this);

            // PaddleOCR travaille de façon bloquante et occupe le processeur. On le
            // sort du fil appelant pour ne pas figer l'API pendant la lecture.
            return Task.Run(() => Lire(image, jetonAnnulation), jetonAnnulation);
        }

        /// <summary>
        /// Libère le moteur PaddleOCR et les modèles qu'il retient en mémoire.
        /// </summary>
        public void Dispose()
        {
            if (libere)
            {
                return;
            }

            moteur.Dispose();
            libere = true;

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Méthodes privées

        private IReadOnlyList<ZoneDeTexte> Lire(byte[] image, CancellationToken jetonAnnulation)
        {
            jetonAnnulation.ThrowIfCancellationRequested();

            using Mat planche = Cv2.ImDecode(image, ImreadModes.Color);

            if (planche.Empty())
            {
                throw new ArgumentException(
                    "Les octets fournis ne forment pas une image décodable.", nameof(image));
            }

            PaddleOcrResult resultat = moteur.Run(planche);

            jetonAnnulation.ThrowIfCancellationRequested();

            List<ZoneDeTexte> zones = new List<ZoneDeTexte>();

            foreach (PaddleOcrResultRegion region in resultat.Regions)
            {
                if (string.IsNullOrWhiteSpace(region.Text))
                {
                    continue;
                }

                if (region.Score < confianceMinimale)
                {
                    continue;
                }

                zones.Add(ConvertirEnZone(region));
            }

            return zones;
        }

        private static ZoneDeTexte ConvertirEnZone(PaddleOcrResultRegion region)
        {
            ZoneDeTexte zone = new ZoneDeTexte();

            zone.Quadrilatere = VersQuadrilatere(region.Rect);
            zone.TexteOriginal = region.Text.Trim();
            zone.Confiance = region.Score;

            // La bulle, le texte traduit et l'ordre de lecture relèvent d'étapes
            // ultérieures : le lecteur ne doit surtout pas y toucher.
            return zone;
        }

        private static Quadrilatere VersQuadrilatere(RotatedRect rectangle)
        {
            // OpenCvSharp ne garantit pas dans quel ordre il rend les quatre coins.
            // On les replace nous-mêmes : les deux plus hauts forment le côté haut,
            // et à l'intérieur de chaque paire, le plus à gauche vient en premier.
            Point2f[] coins = rectangle.Points();

            Point2f[] parHauteur = coins.OrderBy(coin => coin.Y).ToArray();
            Point2f[] haut = parHauteur.Take(2).OrderBy(coin => coin.X).ToArray();
            Point2f[] bas = parHauteur.Skip(2).OrderBy(coin => coin.X).ToArray();

            return new Quadrilatere(
                new Coordonnee(haut[0].X, haut[0].Y),
                new Coordonnee(haut[1].X, haut[1].Y),
                new Coordonnee(bas[1].X, bas[1].Y),
                new Coordonnee(bas[0].X, bas[0].Y));
        }

        #endregion
    }
}
