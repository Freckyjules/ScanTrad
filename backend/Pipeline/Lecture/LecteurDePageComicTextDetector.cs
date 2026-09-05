using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace ScanTrad.Pipeline.Lecture
{
    /// <summary>
    /// Lit une planche en combinant trois outils locaux : le modèle ONNX
    /// comic-text-detector pour situer les lignes, PaddleOCR pour les lire, et
    /// OpenCV pour retrouver le contour de la bulle qui entoure chaque ligne.
    /// </summary>
    /// <remarks>
    /// comic-text-detector est entraîné sur des planches de manga, là où PaddleOCR
    /// est généraliste. Il repère mieux le texte de dialogue, mais il ne sait pas
    /// le lire — d'où la combinaison. C'est un détail d'implémentation : de
    /// l'extérieur, cette classe honore le même contrat que n'importe quel autre
    /// lecteur.
    /// <para>
    /// Le modèle ne fournit <em>pas</em> le contour des bulles, contrairement à ce
    /// qu'on lit souvent. Il sort une carte des lignes de texte, un masque de
    /// l'encre des lettres et des boîtes de blocs. Le contour de bulle est donc
    /// reconstruit ici par remplissage par diffusion : on part du texte, on
    /// progresse dans les pixels clairs, et on s'arrête sur le trait du contour.
    /// </para>
    /// <para>
    /// La sortie reste brute : une zone par ligne détectée. Deux lignes d'une même
    /// bulle porteront donc chacune la même bulle, et ce n'est pas un défaut —
    /// regrouper n'est pas le travail d'un lecteur.
    /// </para>
    /// </remarks>
    public class LecteurDePageComicTextDetector : ILecteurDePage
    {
        #region Constantes

        /// <summary>
        /// Côté de l'image carrée qu'attend le modèle, en pixels.
        /// </summary>
        private const int CoteAttenduParLeModele = 1024;

        /// <summary>
        /// Au-dessus de cette valeur, un pixel de la carte des lignes est considéré
        /// comme appartenant à une ligne de texte.
        /// </summary>
        private const float SeuilDeLaCarteDesLignes = 0.4f;

        /// <summary>
        /// Une ligne plus petite que ça, dans le repère du modèle, est du bruit.
        /// </summary>
        private const int CoteMinimalDuneLigne = 6;

        #endregion

        #region Attributs

        private InferenceSession detecteur;
        private PaddleOcrRecognizer lecteur;
        private double confianceMinimale;
        private bool libere;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un lecteur à partir du fichier du modèle, avec un seuil de
        /// confiance courant.
        /// </summary>
        /// <param name="cheminDuModele">
        /// Chemin du fichier <c>comictextdetector.onnx</c>. Il pèse une centaine de
        /// mégaoctets et n'est pas versionné : il se télécharge à part.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="cheminDuModele"/> est vide.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Levée si le fichier du modèle est introuvable.
        /// </exception>
        public LecteurDePageComicTextDetector(string cheminDuModele)
            : this(cheminDuModele, 0.5)
        {
        }

        /// <summary>
        /// Initialise un lecteur à partir du fichier du modèle, avec un seuil de
        /// confiance choisi.
        /// </summary>
        /// <param name="cheminDuModele">
        /// Chemin du fichier <c>comictextdetector.onnx</c>.
        /// </param>
        /// <param name="confianceMinimale">
        /// En dessous de ce seuil, une ligne lue est jetée sans être remontée.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="cheminDuModele"/> est vide.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Levée si <paramref name="confianceMinimale"/> sort de l'intervalle 0-1.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Levée si le fichier du modèle est introuvable.
        /// </exception>
        public LecteurDePageComicTextDetector(string cheminDuModele, double confianceMinimale)
        {
            if (string.IsNullOrWhiteSpace(cheminDuModele))
            {
                throw new ArgumentException("Le chemin du modèle est obligatoire.", nameof(cheminDuModele));
            }

            if (confianceMinimale < 0 || confianceMinimale > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(confianceMinimale), "Le seuil de confiance se situe entre 0 et 1.");
            }

            if (!File.Exists(cheminDuModele))
            {
                throw new FileNotFoundException(
                    "Le modèle comic-text-detector est introuvable.", cheminDuModele);
            }

            this.confianceMinimale = confianceMinimale;
            this.libere = false;
            this.detecteur = new InferenceSession(cheminDuModele);
            this.lecteur = new PaddleOcrRecognizer(LocalRecognizationModel.EnglishV5);
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

            return Task.Run(() => Lire(image, jetonAnnulation), jetonAnnulation);
        }

        /// <summary>
        /// Libère le modèle ONNX et le moteur de lecture retenus en mémoire.
        /// </summary>
        public void Dispose()
        {
            if (libere)
            {
                return;
            }

            detecteur.Dispose();
            lecteur.Dispose();
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

            using Mat carteDesLignes = DetecterLesLignes(planche);

            jetonAnnulation.ThrowIfCancellationRequested();

            List<Rect> boites = ExtraireLesBoites(carteDesLignes, planche.Size());

            if (boites.Count == 0)
            {
                return new List<ZoneDeTexte>();
            }

            PaddleOcrRecognizerResult[] textes = LireLesBoites(planche, boites);

            jetonAnnulation.ThrowIfCancellationRequested();

            List<ZoneDeTexte> zones = new List<ZoneDeTexte>();

            for (int i = 0; i < boites.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(textes[i].Text) || textes[i].Score < confianceMinimale)
                {
                    continue;
                }

                ZoneDeTexte zone = new ZoneDeTexte();

                zone.Quadrilatere = Quadrilatere.DepuisRectangle(
                    boites[i].X, boites[i].Y, boites[i].Width, boites[i].Height);
                zone.TexteOriginal = textes[i].Text.Trim();
                zone.Confiance = textes[i].Score;
                zone.Bulle = TrouverLaBulle(planche, boites[i]);

                zones.Add(zone);
            }

            return zones;
        }

        private Mat DetecterLesLignes(Mat planche)
        {
            using Mat carre = new Mat();
            Cv2.Resize(planche, carre, new Size(CoteAttenduParLeModele, CoteAttenduParLeModele));
            Cv2.CvtColor(carre, carre, ColorConversionCodes.BGR2RGB);

            DenseTensor<float> entree = new DenseTensor<float>(
                new[] { 1, 3, CoteAttenduParLeModele, CoteAttenduParLeModele });

            for (int y = 0; y < CoteAttenduParLeModele; y++)
            {
                for (int x = 0; x < CoteAttenduParLeModele; x++)
                {
                    Vec3b pixel = carre.At<Vec3b>(y, x);

                    entree[0, 0, y, x] = pixel.Item0 / 255f;
                    entree[0, 1, y, x] = pixel.Item1 / 255f;
                    entree[0, 2, y, x] = pixel.Item2 / 255f;
                }
            }

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> resultat =
                detecteur.Run(new[] { NamedOnnxValue.CreateFromTensor("images", entree) });

            // « det » porte deux canaux ; le premier est la carte des lignes de texte,
            // une barre allumée par ligne. C'est de là que viennent nos boîtes.
            Tensor<float> det = resultat.First(sortie => sortie.Name == "det").AsTensor<float>();

            Mat carte = new Mat(CoteAttenduParLeModele, CoteAttenduParLeModele, MatType.CV_8UC1);

            for (int y = 0; y < CoteAttenduParLeModele; y++)
            {
                for (int x = 0; x < CoteAttenduParLeModele; x++)
                {
                    carte.Set<byte>(y, x, det[0, 0, y, x] > SeuilDeLaCarteDesLignes ? (byte)255 : (byte)0);
                }
            }

            return carte;
        }

        private static List<Rect> ExtraireLesBoites(Mat carteDesLignes, Size tailleReelle)
        {
            Cv2.FindContours(
                carteDesLignes,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            double facteurX = (double)tailleReelle.Width / CoteAttenduParLeModele;
            double facteurY = (double)tailleReelle.Height / CoteAttenduParLeModele;

            List<Rect> boites = new List<Rect>();

            foreach (Point[] contour in contours)
            {
                Rect boite = Cv2.BoundingRect(contour);

                if (boite.Width < CoteMinimalDuneLigne || boite.Height < CoteMinimalDuneLigne / 2)
                {
                    continue;
                }

                boites.Add(new Rect(
                    (int)(boite.X * facteurX),
                    (int)(boite.Y * facteurY),
                    (int)(boite.Width * facteurX),
                    (int)(boite.Height * facteurY)));
            }

            // De haut en bas, pour que la sortie soit au moins lisible. Ce n'est pas
            // l'ordre de lecture d'un manga : ce calcul-là ne nous appartient pas.
            return boites.OrderBy(boite => boite.Y).ThenBy(boite => boite.X).ToList();
        }

        private PaddleOcrRecognizerResult[] LireLesBoites(Mat planche, List<Rect> boites)
        {
            Mat[] decoupes = new Mat[boites.Count];

            try
            {
                for (int i = 0; i < boites.Count; i++)
                {
                    // La carte des lignes du modèle est plus étroite que le texte
                    // réel : sans marge, le dernier caractère est rogné et « MAGIC
                    // ACADEMY » ressort en « WAGIC ACADEM ». La marge suit la hauteur
                    // de la ligne, pour rester juste quelle que soit la taille.
                    int marge = Math.Max(6, boites[i].Height / 4);

                    decoupes[i] = new Mat(planche, Elargir(boites[i], planche.Size(), marge));
                }

                // Une seule passe pour toutes les découpes : le moteur les traite par
                // paquets, ce qui est nettement plus rapide qu'un appel par ligne.
                return lecteur.Run(decoupes, 0);
            }
            finally
            {
                foreach (Mat decoupe in decoupes)
                {
                    decoupe?.Dispose();
                }
            }
        }

        private static Bulle? TrouverLaBulle(Mat planche, Rect boiteDuTexte)
        {
            using Mat gris = new Mat();
            Cv2.CvtColor(planche, gris, ColorConversionCodes.BGR2GRAY);

            Point? depart = ChercherUnPixelClairAutourDuTexte(gris, boiteDuTexte);

            if (depart == null)
            {
                return null;
            }

            // Le masque du remplissage doit déborder d'un pixel de chaque côté :
            // c'est OpenCV qui l'impose, il s'en sert de garde-fou.
            using Mat masque = new Mat(planche.Height + 2, planche.Width + 2, MatType.CV_8UC1, Scalar.All(0));

            FloodFillFlags drapeaux = FloodFillFlags.MaskOnly
                | (FloodFillFlags)4
                | (FloodFillFlags)(255 << 8);

            Cv2.FloodFill(
                gris,
                masque,
                depart.Value,
                Scalar.All(255),
                out Rect etendue,
                Scalar.All(28),
                Scalar.All(28),
                drapeaux);

            if (!LEtendueEstCredible(etendue, boiteDuTexte, planche.Size()))
            {
                return null;
            }

            using Mat interieur = new Mat(masque, new Rect(1, 1, planche.Width, planche.Height));

            Cv2.FindContours(
                interieur,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0)
            {
                return null;
            }

            Point[] leplusGrand = contours.OrderByDescending(contour => Cv2.ContourArea(contour)).First();

            // Le contour brut compte des centaines de points collés. On le simplifie
            // pour obtenir un polygone maniable, éditable à la souris depuis le front.
            double tolerance = 0.004 * Cv2.ArcLength(leplusGrand, true);
            Point[] simplifie = Cv2.ApproxPolyDP(leplusGrand, tolerance, true);

            if (simplifie.Length < 3)
            {
                return null;
            }

            return new Bulle(simplifie.Select(point => new Coordonnee(point.X, point.Y)));
        }

        private static Point? ChercherUnPixelClairAutourDuTexte(Mat gris, Rect boiteDuTexte)
        {
            // On cherche du blanc de bulle autour du texte sans tomber sur les lettres.
            // Plusieurs distances sont tentées : au-dessus d'une ligne du milieu, une
            // marge courte tombe dans l'interligne — qui est blanc — alors qu'une
            // marge longue atteint la ligne précédente, qui ne l'est pas.

            int[] marges =
            {
                Math.Max(2, boiteDuTexte.Height / 6),
                Math.Max(4, boiteDuTexte.Height / 3),
                Math.Max(6, boiteDuTexte.Height / 2)
            };

            // Un seul point par direction est trop fragile : au-dessus d'un « Y », on
            // tombe sur la lettre alors qu'il y a du blanc deux pixels à côté. On
            // balaie donc la largeur et la hauteur de la boîte.
            const int NombreDeSondes = 7;

            foreach (int marge in marges)
            {
                for (int sonde = 1; sonde < NombreDeSondes; sonde++)
                {
                    int surLaLargeur = boiteDuTexte.X + (boiteDuTexte.Width * sonde / NombreDeSondes);
                    int surLaHauteur = boiteDuTexte.Y + (boiteDuTexte.Height * sonde / NombreDeSondes);

                    Point[] candidats =
                    {
                        new Point(surLaLargeur, boiteDuTexte.Y - marge),
                        new Point(surLaLargeur, boiteDuTexte.Bottom + marge),
                        new Point(boiteDuTexte.X - marge, surLaHauteur),
                        new Point(boiteDuTexte.Right + marge, surLaHauteur)
                    };

                    foreach (Point candidat in candidats)
                    {
                        if (candidat.X < 0 || candidat.Y < 0 || candidat.X >= gris.Width || candidat.Y >= gris.Height)
                        {
                            continue;
                        }

                        if (gris.At<byte>(candidat.Y, candidat.X) >= 200)
                        {
                            return candidat;
                        }
                    }
                }
            }

            return null;
        }

        private static bool LEtendueEstCredible(Rect etendue, Rect boiteDuTexte, Size taillePlanche)
        {
            double airePlanche = (double)taillePlanche.Width * taillePlanche.Height;
            double aireEtendue = (double)etendue.Width * etendue.Height;

            // Trop grand : le remplissage s'est échappé par une ouverture du contour
            // et a envahi la planche. Trop petit : on n'a attrapé qu'un interligne.
            if (aireEtendue > airePlanche * 0.25)
            {
                return false;
            }

            return etendue.Width >= boiteDuTexte.Width && etendue.Height >= boiteDuTexte.Height;
        }

        private static Rect Elargir(Rect boite, Size limites, int marge)
        {
            int x = Math.Max(0, boite.X - marge);
            int y = Math.Max(0, boite.Y - marge);
            int droite = Math.Min(limites.Width, boite.Right + marge);
            int bas = Math.Min(limites.Height, boite.Bottom + marge);

            return new Rect(x, y, Math.Max(1, droite - x), Math.Max(1, bas - y));
        }

        #endregion
    }
}
