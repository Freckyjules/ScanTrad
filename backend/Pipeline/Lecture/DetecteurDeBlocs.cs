using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace ScanTrad.Pipeline.Lecture
{
    /// <summary>
    /// Localise les blocs de dialogue d'une planche avec le modèle
    /// comic-text-detector.
    /// </summary>
    /// <remarks>
    /// Le modèle rend trois choses : des boîtes de blocs, une carte des lignes et un
    /// masque de l'encre. On n'utilise ici que les boîtes de blocs, parce qu'elles
    /// sont déjà groupées par bulle et qu'elles ignorent les onomatopées — deux
    /// travaux qu'on n'a donc pas à refaire.
    /// <para>
    /// Le modèle ne détecte ni les bulles ni le texte lui-même : il dit seulement où
    /// se trouvent les blocs.
    /// </para>
    /// </remarks>
    public class DetecteurDeBlocs : IDisposable
    {
        #region Constantes

        /// <summary>
        /// Côté de l'image carrée qu'attend le modèle, en pixels.
        /// </summary>
        private const int CoteAttenduParLeModele = 1024;

        /// <summary>
        /// En dessous de cette certitude, une détection est du bruit.
        /// </summary>
        private const float CertitudeMinimale = 0.5f;

        /// <summary>
        /// Part de recouvrement au-delà de laquelle deux détections sont considérées
        /// comme la même, la moins sûre étant écartée.
        /// </summary>
        private const double RecouvrementDunDoublon = 0.4;

        #endregion

        #region Attributs

        private InferenceSession session;
        private bool libere;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Charge le modèle depuis son fichier.
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
        public DetecteurDeBlocs(string cheminDuModele)
        {
            if (string.IsNullOrWhiteSpace(cheminDuModele))
            {
                throw new ArgumentException("Le chemin du modèle est obligatoire.", nameof(cheminDuModele));
            }

            if (!File.Exists(cheminDuModele))
            {
                throw new FileNotFoundException(
                    "Le modèle comic-text-detector est introuvable.", cheminDuModele);
            }

            this.session = new InferenceSession(cheminDuModele);
            this.libere = false;
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Trouve les blocs de dialogue d'une planche.
        /// </summary>
        /// <param name="planche">La planche à analyser, en couleurs.</param>
        /// <returns>
        /// Les boîtes des blocs, en pixels de la planche, du plus haut au plus bas.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        public IReadOnlyList<Rect> Detecter(Mat planche)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            ObjectDisposedException.ThrowIf(libere, this);

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> resultat =
                session.Run(new[] { NamedOnnxValue.CreateFromTensor("images", Preparer(planche)) });

            Tensor<float> blocs = resultat.First(sortie => sortie.Name == "blk").AsTensor<float>();

            return EcarterLesDoublons(Decoder(blocs, planche.Size()));
        }

        /// <summary>
        /// Libère le modèle retenu en mémoire.
        /// </summary>
        public void Dispose()
        {
            if (libere)
            {
                return;
            }

            session.Dispose();
            libere = true;

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Méthodes privées

        private static DenseTensor<float> Preparer(Mat planche)
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

            return entree;
        }

        private static List<(Rect Boite, float Certitude)> Decoder(Tensor<float> blocs, Size taillePlanche)
        {
            // Chaque ancre porte sept nombres : centre, taille, certitude, puis deux
            // probabilités de classe dont on ne sait pas ce qu'elles distinguent.
            double facteurX = (double)taillePlanche.Width / CoteAttenduParLeModele;
            double facteurY = (double)taillePlanche.Height / CoteAttenduParLeModele;

            List<(Rect, float)> candidats = new List<(Rect, float)>();

            for (int i = 0; i < blocs.Dimensions[1]; i++)
            {
                float certitude = blocs[0, i, 4];

                if (certitude < CertitudeMinimale)
                {
                    continue;
                }

                float centreX = blocs[0, i, 0];
                float centreY = blocs[0, i, 1];
                float largeur = blocs[0, i, 2];
                float hauteur = blocs[0, i, 3];

                candidats.Add((
                    new Rect(
                        (int)((centreX - (largeur / 2)) * facteurX),
                        (int)((centreY - (hauteur / 2)) * facteurY),
                        (int)(largeur * facteurX),
                        (int)(hauteur * facteurY)),
                    certitude));
            }

            return candidats;
        }

        private static List<Rect> EcarterLesDoublons(List<(Rect Boite, float Certitude)> candidats)
        {
            // Des dizaines d'ancres décrivent le même bloc : on parcourt de la plus
            // sûre à la moins sûre et on jette celles qui recouvrent une retenue.
            List<Rect> retenues = new List<Rect>();

            foreach ((Rect boite, float _) in candidats.OrderByDescending(candidat => candidat.Certitude))
            {
                if (!retenues.Any(retenue => SeRecouvrent(retenue, boite)))
                {
                    retenues.Add(boite);
                }
            }

            return retenues
                .OrderBy(boite => boite.Y)
                .ThenBy(boite => boite.X)
                .ToList();
        }

        private static bool SeRecouvrent(Rect premiere, Rect seconde)
        {
            Rect commune = premiere & seconde;

            double aireCommune = commune.Width * (double)commune.Height;
            double laPlusPetite = Math.Min(
                premiere.Width * (double)premiere.Height,
                seconde.Width * (double)seconde.Height);

            return laPlusPetite > 0 && aireCommune > RecouvrementDunDoublon * laPlusPetite;
        }

        #endregion
    }
}
