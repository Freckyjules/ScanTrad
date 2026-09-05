using OpenCvSharp;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Effacement
{
    /// <summary>
    /// Efface le texte en remplissant l'intérieur de chaque bulle d'une couleur unie.
    /// </summary>
    /// <remarks>
    /// La méthode la plus simple qui marche, et elle marche sur l'immense majorité
    /// des planches : une bulle de manga est unie, donc la repeindre de sa propre
    /// couleur efface le texte sans laisser de trace.
    /// <para>
    /// Cette couleur est celle que la lecture a mesurée à l'intérieur de la bulle, et
    /// non un blanc décrété. Une bulle blanche donne rarement 255 : les scans tirent
    /// vers le crème ou le gris, et un remplissage en blanc pur y ferait une tache
    /// plus claire que le reste de la bulle. La couleur réglée sur l'effaceur ne sert
    /// que de repli, là où rien n'a été mesuré.
    /// </para>
    /// <para>
    /// Elle montre ses limites sur les bulles tramées ou en dégradé, où le
    /// remplissage uni se verra. Le remède, plus tard, est l'inpainting : n'effacer
    /// que les pixels d'encre des lettres et reconstituer le fond autour. Le masque
    /// de texte que sort déjà comic-text-detector est fait pour ça.
    /// </para>
    /// <para>
    /// L'image nettoyée est encodée en PNG, sans perte. Le JPEG produirait des
    /// artefacts autour des traits noirs et du texte — exactement ce dont une planche
    /// de manga est faite — et on ré-encoderait par-dessus une image déjà compressée.
    /// </para>
    /// </remarks>
    public class EffaceurParRemplissage : IEffaceurDeTexte
    {
        #region Attributs

        private Scalar couleur;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un effaceur qui se rabat sur le blanc pour les bulles dont le
        /// fond n'a pas été mesuré.
        /// </summary>
        public EffaceurParRemplissage()
            : this(Scalar.All(255))
        {
        }

        /// <summary>
        /// Initialise un effaceur avec la couleur de repli indiquée.
        /// </summary>
        /// <param name="couleur">
        /// La couleur dont on repeint les bulles dont le fond n'a pas été mesuré, en
        /// bleu-vert-rouge.
        /// </param>
        public EffaceurParRemplissage(Scalar couleur)
        {
            this.couleur = couleur;
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// La couleur de repli, dont on repeint les bulles dont la lecture n'a pas
        /// mesuré le fond. Quand <see cref="ZoneDeTexte.CouleurDeFond"/> est
        /// renseignée, c'est elle qui sert.
        /// </summary>
        public Scalar Couleur
        {
            get { return couleur; }
            set { couleur = value; }
        }

        #endregion

        #region Méthodes

        /// <inheritdoc />
        public Planche Effacer(Planche planche)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            if (planche.Image.Length == 0)
            {
                throw new ArgumentException("L'image de la planche est vide.", nameof(planche));
            }

            using Mat nettoyee = Cv2.ImDecode(planche.Image, ImreadModes.Color);

            if (nettoyee.Empty())
            {
                throw new ArgumentException(
                    "Les octets de la planche ne forment pas une image décodable.", nameof(planche));
            }

            foreach (ZoneDeTexte zone in planche.Zones)
            {
                if (zone.Bulle == null || zone.Bulle.Contour.Count < 3)
                {
                    // Sans bulle, on ne sait pas jusqu'où effacer sans mordre sur le
                    // dessin. On laisse la zone telle quelle.
                    continue;
                }

                Cv2.FillPoly(
                    nettoyee, new[] { VersContourOpenCv(zone.Bulle) }, Teinte(zone), LineTypes.AntiAlias);
            }

            // ImDecode a produit une copie : la planche reçue n'a pas été touchée, et
            // l'appelante garde son image d'origine intacte.
            return planche.AvecImage(nettoyee.ImEncode(".png"));
        }

        #endregion

        #region Méthodes privées

        private Scalar Teinte(ZoneDeTexte zone)
        {
            // La couleur mesurée sur la planche l'emporte : un scan tire vers le crème
            // ou le gris, et repeindre en blanc pur y laisserait une tache plus claire
            // que le reste de la bulle. La couleur réglée ne sert que là où la lecture
            // n'a rien mesuré.
            if (zone.CouleurDeFond == null)
            {
                return couleur;
            }

            Couleur fond = zone.CouleurDeFond;

            return new Scalar(fond.Bleu, fond.Vert, fond.Rouge);
        }

        private static Point[] VersContourOpenCv(Bulle bulle)
        {
            Point[] contour = new Point[bulle.Contour.Count];

            for (int i = 0; i < contour.Length; i++)
            {
                Coordonnee point = bulle.Contour[i];

                contour[i] = new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
            }

            return contour;
        }

        #endregion
    }
}
