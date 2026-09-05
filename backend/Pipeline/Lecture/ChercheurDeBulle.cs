using OpenCvSharp;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Lecture
{
    /// <summary>
    /// Retrouve le contour de la bulle qui entoure un bloc de texte, par diffusion
    /// dans les pixels clairs, et mesure la couleur de son papier.
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
    /// <para>
    /// La couleur du fond se mesure ici et pas ailleurs, parce que le masque de la
    /// diffusion n'existe qu'ici. Il tombe bien : la diffusion s'arrête sur l'encre,
    /// donc ce masque est exactement le papier de la bulle, lettres exclues.
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
        /// Cherche le contour de la bulle entourant un bloc de texte, et mesure au
        /// passage la couleur de son papier.
        /// </summary>
        /// <remarks>
        /// Les deux vont ensemble parce que la couleur se mesure sur le masque de la
        /// diffusion, qui n'existe qu'ici : ce qui ressort est un contour, et les
        /// pixels qu'il enferme sont perdus ensuite.
        /// </remarks>
        /// <param name="gris">La planche en niveaux de gris, où se fait la diffusion.</param>
        /// <param name="couleur">
        /// La même planche en couleur, d'où se lit la couleur du fond. Un niveau de
        /// gris ne suffirait pas : un beige et un gris bleuté de même luminosité
        /// donnent le même octet.
        /// </param>
        /// <param name="bloc">La boîte du bloc de texte.</param>
        /// <param name="couleurDeFond">
        /// La couleur du papier à l'intérieur de la bulle, ou <c>null</c> s'il n'y a
        /// pas de bulle.
        /// </param>
        /// <returns>
        /// La bulle trouvée, ou <c>null</c> s'il n'y en a pas — une onomatopée
        /// dessinée à même la planche n'en a aucune.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="gris"/> ou <paramref name="couleur"/> vaut
        /// <c>null</c>.
        /// </exception>
        public static Bulle? Chercher(Mat gris, Mat couleur, Rect bloc, out Couleur? couleurDeFond)
        {
            if (gris == null)
            {
                throw new ArgumentNullException(nameof(gris));
            }

            if (couleur == null)
            {
                throw new ArgumentNullException(nameof(couleur));
            }

            couleurDeFond = null;

            using Mat? brut = Remplir(gris, bloc);

            if (brut == null)
            {
                return null;
            }

            Point[]? contour = ChoisirLeContour(brut, bloc);

            if (contour == null)
            {
                return null;
            }

            couleurDeFond = MesurerLeFond(couleur, brut, contour);

            return new Bulle(contour.Select(point => new Coordonnee(point.X, point.Y)));
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

        private static Couleur? MesurerLeFond(Mat couleur, Mat brut, Point[] contour)
        {
            // On croise deux masques. Celui de la diffusion ne contient que le papier :
            // l'encre des lettres l'a arrêtée, elles n'y sont donc pas, et la moyenne
            // ne se fait pas tirer vers le noir. Celui du contour retenu écarte ce
            // qu'une fuite aurait ramassé dans le décor avant que l'ouverture ne la
            // coupe.
            using Mat interieur = Mat.Zeros(brut.Size(), MatType.CV_8UC1).ToMat();

            Cv2.FillPoly(interieur, new[] { contour }, Scalar.All(255));
            Cv2.BitwiseAnd(interieur, brut, interieur);

            if (Cv2.CountNonZero(interieur) == 0)
            {
                return null;
            }

            Scalar moyenne = Cv2.Mean(couleur, interieur);

            // OpenCV range les canaux en bleu-vert-rouge.
            return new Couleur(
                Arrondir(moyenne.Val2),
                Arrondir(moyenne.Val1),
                Arrondir(moyenne.Val0));
        }

        private static int Arrondir(double composante)
        {
            // Une moyenne ne peut pas sortir de l'intervalle des valeurs moyennées,
            // mais le bornage coûte moins cher qu'une exception un jour de surprise.
            return Math.Clamp((int)Math.Round(composante), 0, 255);
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

            // Le contour est rendu tel quel, sans le simplifier. Un polygone simplifié
            // coupe les virages en ligne droite, et sur une bulle ces raccourcis
            // passent à travers son trait : l'effacement mordait alors dessus. Mesuré
            // sur la planche d'essai, la clarté moyenne du bord tombait à 127-165 sur
            // quatre bulles sur huit ; sans simplification, les huit sont à 251-253,
            // c'est-à-dire franchement sur le papier.
            //
            // Le contour coûte alors 500 à 850 points au lieu d'une quarantaine, ce
            // qui ne se voit ni au chronomètre — la lecture est dominée par l'OCR — ni
            // en mémoire. La simplification n'existait que pour rendre le polygone
            // maniable à la souris ; personne ne l'édite.
            return leplusGrand.Length >= 3 ? leplusGrand : null;
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
