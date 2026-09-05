using OpenCvSharp;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.PipelineTests
{
    /// <summary>
    /// Dessine sur une planche ce que le pipeline y a trouvé, et attache l'image au
    /// test en cours pour qu'elle s'ouvre depuis l'explorateur de tests.
    /// </summary>
    /// <remarks>
    /// Aucune assertion ne dit si une détection est <em>juste</em> : il faut la
    /// regarder. C'est pour ça que plusieurs tests sur planche réelle ne vérifient
    /// presque rien — leur vrai produit est cette image.
    /// <para>
    /// Le numéro posé sur chaque bloc est son rang de lecture dès qu'il est calculé,
    /// et son rang de détection sinon. Quand toutes les zones portent un rang, le
    /// chemin qui les relie est tracé par-dessus : c'est ce qui rend un ordre de
    /// lecture vérifiable d'un coup d'œil, là où une liste de textes demande de
    /// retrouver soi-même quelle bulle est laquelle.
    /// </para>
    /// </remarks>
    public static class ApercuDePlanche
    {
        #region Constantes

        /// <summary>
        /// Les couleurs s'écrivent en bleu-vert-rouge, et non l'inverse : c'est
        /// l'ordre des octets d'OpenCV.
        /// </summary>
        private static readonly Scalar CouleurDeLaBulle = new Scalar(0, 0, 255);

        private static readonly Scalar CouleurDuRectangle = new Scalar(0, 200, 0);
        private static readonly Scalar CouleurDuNumero = new Scalar(255, 60, 0);
        private static readonly Scalar CouleurDuChemin = new Scalar(255, 0, 255);

        #endregion

        #region Méthodes

        /// <summary>
        /// Dessine l'aperçu de la planche et l'attache au test en cours.
        /// </summary>
        /// <param name="nom">
        /// Le nom sous lequel la pièce jointe apparaît dans l'explorateur de tests.
        /// </param>
        /// <param name="planche">La planche à représenter, avec ses zones.</param>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Levée si l'image de la planche n'est pas décodable.
        /// </exception>
        public static void Attacher(string nom, Planche planche)
        {
            TestContext.Current.AddAttachment(nom, Dessiner(planche), "image/png");
        }

        /// <summary>
        /// Dessine sur la planche le rectangle de chaque bloc, le contour de sa bulle,
        /// son numéro, et le chemin de lecture si les rangs sont connus.
        /// </summary>
        /// <param name="planche">La planche à représenter, avec ses zones.</param>
        /// <returns>L'image annotée, encodée en PNG.</returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Levée si l'image de la planche n'est pas décodable.
        /// </exception>
        public static byte[] Dessiner(Planche planche)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            using Mat dessin = Cv2.ImDecode(planche.Image, ImreadModes.Color);

            if (dessin.Empty())
            {
                throw new ArgumentException(
                    "L'image de la planche n'est pas décodable.", nameof(planche));
            }

            // Le chemin passe dessous : il relie des centres, et les cadres doivent
            // rester lisibles par-dessus.
            DessinerLeChemin(dessin, planche.Zones);

            for (int rang = 0; rang < planche.Zones.Count; rang++)
            {
                ZoneDeTexte zone = planche.Zones[rang];

                DessinerLaBulle(dessin, zone.Bulle);
                DessinerLeRectangle(dessin, zone.Rectangle);
                DessinerLeNumero(dessin, zone.Rectangle, zone.OrdreDeLecture ?? rang);
            }

            return dessin.ImEncode(".png");
        }

        #endregion

        #region Méthodes privées

        private static void DessinerLeChemin(Mat dessin, IReadOnlyList<ZoneDeTexte> zones)
        {
            // Sans rang partout, un chemin relierait des blocs dans un ordre qui n'a
            // pas de sens. Mieux vaut ne rien tracer que tracer un ordre inventé.
            if (zones.Count < 2 || zones.Any(zone => zone.OrdreDeLecture == null))
            {
                return;
            }

            List<ZoneDeTexte> parRang = zones
                .OrderBy(zone => zone.OrdreDeLecture!.Value)
                .ToList();

            for (int i = 1; i < parRang.Count; i++)
            {
                Cv2.ArrowedLine(
                    dessin,
                    Vers(parRang[i - 1].Rectangle.Centre),
                    Vers(parRang[i].Rectangle.Centre),
                    CouleurDuChemin,
                    thickness: 5,
                    lineType: LineTypes.AntiAlias,
                    shift: 0,
                    tipLength: 0.04);
            }
        }

        private static void DessinerLaBulle(Mat dessin, Bulle? bulle)
        {
            if (bulle == null || bulle.Contour.Count < 3)
            {
                return;
            }

            Point[] contour = bulle.Contour.Select(point => Vers(point)).ToArray();

            Cv2.Polylines(dessin, new[] { contour }, isClosed: true, CouleurDeLaBulle, 5);
        }

        private static void DessinerLeRectangle(Mat dessin, Quadrilatere rectangle)
        {
            Point[] coins =
            {
                Vers(rectangle.HautGauche),
                Vers(rectangle.HautDroit),
                Vers(rectangle.BasDroit),
                Vers(rectangle.BasGauche)
            };

            Cv2.Polylines(dessin, new[] { coins }, isClosed: true, CouleurDuRectangle, 4);
        }

        private static void DessinerLeNumero(Mat dessin, Quadrilatere rectangle, int numero)
        {
            Point ancre = Vers(rectangle.HautGauche);

            Cv2.PutText(
                dessin,
                numero.ToString(),
                new Point(ancre.X, Math.Max(40, ancre.Y - 14)),
                HersheyFonts.HersheySimplex,
                1.4,
                CouleurDuNumero,
                4);
        }

        private static Point Vers(Coordonnee point)
        {
            return new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
        }

        #endregion
    }
}
