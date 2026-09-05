using OpenCvSharp;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Lecture
{
    /// <summary>
    /// Retrouve le contour de la bulle qui entoure un bloc de texte, par diffusion
    /// dans les pixels clairs.
    /// </summary>
    /// <remarks>
    /// Aucun modèle ne détecte les bulles : elles sont reconstruites ici par de
    /// l'algorithmique d'image. On part d'un point clair du bloc, on progresse tant
    /// que la luminosité reste voisine, et on s'arrête sur le trait du contour.
    /// <para>
    /// Deux réglages viennent de mesures et méritent qu'on ne les défasse pas :
    /// l'amorce se cherche <em>dans</em> les interlignes du bloc et non autour de
    /// lui, et les filaments se coupent par une ouverture appliquée par paliers.
    /// </para>
    /// </remarks>
    public static class ChercheurDeBulle
    {
        #region Constantes

        /// <summary>
        /// Écart de luminosité toléré par la diffusion. Mesuré sans effet entre 6 et
        /// 28 : les fuites ne passent pas par un trait pâle, inutile de le régler
        /// finement.
        /// </summary>
        private const int Tolerance = 20;

        /// <summary>
        /// Nombre de rangées claires du bloc essayées comme amorce.
        /// </summary>
        private const int NombreDAmorces = 6;

        /// <summary>
        /// Luminosité minimale d'un pixel pour servir d'amorce.
        /// </summary>
        private const int ClarteMinimaleDuneAmorce = 235;

        /// <summary>
        /// Part d'aire perdue à partir de laquelle on considère qu'un goulot a été
        /// rompu. Mesuré : une bulle saine perd moins de 0,1 %, une bulle à filament
        /// entre 22 et 91 %. N'importe quelle valeur entre 1 % et 20 % convient.
        /// </summary>
        private const double ChuteSignificative = 0.15;

        /// <summary>
        /// Part du bloc que le contour doit couvrir pour être crédible.
        /// </summary>
        private const double CouvertureMinimaleDuBloc = 0.8;

        /// <summary>
        /// Rayons d'érosion essayés, du plus doux au plus brutal.
        /// </summary>
        private static readonly int[] Rayons = { 3, 5, 7, 10, 14 };

        #endregion

        #region Méthodes

        /// <summary>
        /// Cherche le contour de la bulle entourant un bloc de texte.
        /// </summary>
        /// <param name="gris">La planche en niveaux de gris.</param>
        /// <param name="bloc">La boîte du bloc de texte.</param>
        /// <returns>
        /// La bulle trouvée, ou <c>null</c> s'il n'y en a pas — une onomatopée
        /// dessinée à même la planche n'en a aucune.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="gris"/> vaut <c>null</c>.
        /// </exception>
        public static Bulle? Chercher(Mat gris, Rect bloc)
        {
            if (gris == null)
            {
                throw new ArgumentNullException(nameof(gris));
            }

            using Mat? brut = Remplir(gris, bloc);

            if (brut == null)
            {
                return null;
            }

            Point[]? contour = ChoisirLeContour(brut, bloc);

            return contour == null
                ? null
                : new Bulle(contour.Select(point => new Coordonnee(point.X, point.Y)));
        }

        #endregion

        #region Méthodes privées

        private static Point[]? ChoisirLeContour(Mat brut, Rect bloc)
        {
            // L'ouverture est presque sans effet sur une forme compacte et ampute une
            // forme reliée par un goulot. On monte donc en rayon tant que chaque cran
            // continue d'amputer, et on s'arrête dès que la forme se stabilise : une
            // bulle saine s'arrête au premier palier et garde ses pointes intactes.
            Point[]? retenu = VersContour(brut, out double aireRetenue);

            if (retenu == null)
            {
                return null;
            }

            foreach (int rayon in Rayons)
            {
                using Mat ouvert = Ouvrir(brut, bloc, rayon);

                Point[]? candidat = VersContour(ouvert, out double aire);

                if (candidat == null || aireRetenue <= 0)
                {
                    break;
                }

                if ((aireRetenue - aire) / aireRetenue <= ChuteSignificative)
                {
                    break;
                }

                retenu = candidat;
                aireRetenue = aire;
            }

            return CouvreLeBloc(retenu, bloc) ? retenu : null;
        }

        private static Mat Ouvrir(Mat masque, Rect bloc, int rayon)
        {
            using Mat noyau = Cv2.GetStructuringElement(
                MorphShapes.Ellipse, new Size((2 * rayon) + 1, (2 * rayon) + 1));

            using Mat erode = new Mat();
            Cv2.Erode(masque, erode, noyau);

            using Mat retenue = GarderLaPartieDuBloc(erode, bloc);

            Mat dilate = new Mat();
            Cv2.Dilate(retenue, dilate, noyau);

            return dilate;
        }

        private static Mat GarderLaPartieDuBloc(Mat erode, Rect bloc)
        {
            // Après érosion, la bulle et ce qui vient d'en être détaché forment deux
            // morceaux. Celui de la bulle est celui qui couvre encore les interlignes.
            using Mat etiquettes = new Mat();
            int nombre = Cv2.ConnectedComponents(erode, etiquettes);

            int meilleure = 0;
            int meilleurCompte = 0;

            for (int etiquette = 1; etiquette < nombre; etiquette++)
            {
                using Mat morceau = new Mat();
                Cv2.Compare(etiquettes, etiquette, morceau, CmpType.EQ);

                using Mat dansLeBloc = new Mat(morceau, bloc);
                int compte = Cv2.CountNonZero(dansLeBloc);

                if (compte > meilleurCompte)
                {
                    meilleurCompte = compte;
                    meilleure = etiquette;
                }
            }

            if (meilleure == 0)
            {
                return Mat.Zeros(erode.Size(), MatType.CV_8UC1).ToMat();
            }

            Mat retenue = new Mat();
            Cv2.Compare(etiquettes, meilleure, retenue, CmpType.EQ);

            return retenue;
        }

        private static Mat? Remplir(Mat gris, Rect bloc)
        {
            foreach (Point amorce in ChercherLesAmorces(gris, bloc))
            {
                using Mat masque = new Mat(gris.Height + 2, gris.Width + 2, MatType.CV_8UC1, Scalar.All(0));

                Cv2.FloodFill(
                    gris,
                    masque,
                    amorce,
                    Scalar.All(255),
                    out Rect _,
                    Scalar.All(Tolerance),
                    Scalar.All(Tolerance),
                    FloodFillFlags.MaskOnly | (FloodFillFlags)4 | (FloodFillFlags)(255 << 8));

                Mat interieur = new Mat(masque, new Rect(1, 1, gris.Width, gris.Height)).Clone();

                if (CouvreLeBloc(VersContour(interieur, out _), bloc))
                {
                    return interieur;
                }

                interieur.Dispose();
            }

            return null;
        }

        private static IEnumerable<Point> ChercherLesAmorces(Mat gris, Rect bloc)
        {
            // Une ligne de texte assombrit sa rangée, un interligne la laisse claire.
            // Sonder dans le bloc plutôt qu'autour garantit qu'on part de la bulle,
            // même si le modèle a réuni deux bulles voisines dans une même boîte.
            List<(int Y, double Clarte)> rangees = new List<(int, double)>();

            for (int y = bloc.Top; y < bloc.Bottom; y++)
            {
                using Mat rangee = new Mat(gris, new Rect(bloc.X, y, bloc.Width, 1));

                rangees.Add((y, Cv2.Mean(rangee).Val0));
            }

            IEnumerable<int> lesPlusClaires = rangees
                .OrderByDescending(rangee => rangee.Clarte)
                .Take(NombreDAmorces)
                .Select(rangee => rangee.Y);

            foreach (int y in lesPlusClaires)
            {
                foreach (int x in new[]
                {
                    bloc.X + (bloc.Width / 2),
                    bloc.X + (bloc.Width / 4),
                    bloc.X + (3 * bloc.Width / 4)
                })
                {
                    if (gris.At<byte>(y, x) >= ClarteMinimaleDuneAmorce)
                    {
                        yield return new Point(x, y);
                        break;
                    }
                }
            }
        }

        private static Point[]? VersContour(Mat masque, out double aire)
        {
            aire = 0;

            Cv2.FindContours(
                masque,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0)
            {
                return null;
            }

            Point[] leplusGrand = contours
                .OrderByDescending(contour => Math.Abs(Cv2.ContourArea(contour)))
                .First();

            aire = Math.Abs(Cv2.ContourArea(leplusGrand));

            // Le contour brut compte des centaines de points collés. On le simplifie
            // pour obtenir un polygone maniable, éditable à la souris depuis le front.
            double toleranceDuTrace = 0.004 * Cv2.ArcLength(leplusGrand, true);
            Point[] simplifie = Cv2.ApproxPolyDP(leplusGrand, toleranceDuTrace, true);

            return simplifie.Length >= 3 ? simplifie : null;
        }

        private static bool CouvreLeBloc(Point[]? contour, Rect bloc)
        {
            if (contour == null)
            {
                return false;
            }

            Rect englobant = Cv2.BoundingRect(contour);

            return englobant.Width >= bloc.Width * CouvertureMinimaleDuBloc
                && englobant.Height >= bloc.Height * CouvertureMinimaleDuBloc;
        }

        #endregion
    }
}
