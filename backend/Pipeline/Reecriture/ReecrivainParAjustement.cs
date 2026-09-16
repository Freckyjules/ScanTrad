using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Reecriture
{
    /// <summary>
    /// Réécrit le texte traduit en partant de la taille du texte d'origine, et ne la
    /// réduit que si la traduction, plus longue, ne tient plus dans le contour de la
    /// bulle.
    /// </summary>
    /// <remarks>
    /// Le rendu passe par GDI+ (<c>System.Drawing</c>) et non par OpenCV : les polices
    /// Hershey qu'utilise <c>Cv2.PutText</c> ne connaissent pas les caractères
    /// accentués du français, et écriraient « r suis-je arriv » à la place de « r suis-je
    /// arrivé ». GDI+ n'existe que sous Windows, ce qui n'ajoute aucune contrainte
    /// nouvelle : le pipeline en dépend déjà par ses autres paquets natifs.
    /// <para>
    /// La boîte de texte est <see cref="ZoneDeTexte.Rectangle"/> tel quel, sans marge
    /// intérieure : le contour de la bulle ne sert plus ici. Il sert déjà ailleurs
    /// dans le pipeline — à l'effacement, et au cadrage qui maximise justement
    /// <c>Rectangle</c> dans ce contour avant que la réécriture n'intervienne (voir
    /// <c>IAjusteurDeRectangle</c>). Lui faire aussi porter la mise en page du texte
    /// serait redondant, et une marge en plus de celle déjà prise par le cadrage
    /// rétrécirait deux fois pour rien.
    /// </para>
    /// <para>
    /// La taille de départ vient de <see cref="ZoneDeTexte.HauteurDeLigne"/> — la
    /// hauteur d'une ligne du texte d'origine, mesurée à la lecture — convertie en
    /// taille de police par un ratio mesuré, et non déclaré : les métriques que GDI+
    /// expose pour une police (ascendant + descendant rapportés à son corps)
    /// surestiment nettement la hauteur d'encre qu'une ligne réelle occupe, ce qui
    /// produisait un texte visiblement trop petit. Le ratio utilisé vient donc du
    /// rendu d'une lettre à une taille de référence, dont on mesure ensuite la
    /// hauteur d'encre réelle. Le français est presque toujours un peu plus long que
    /// l'anglais ou le japonais lu ; l'algorithme découpe donc le texte en lignes à
    /// cette taille, et ne la réduit que si le résultat déborde de la hauteur du
    /// rectangle — jamais pour l'agrandir.
    /// </para>
    /// <para>
    /// Le découpage en lignes est glouton : un mot rejoint la ligne courante s'il y
    /// tient, sinon il ouvre la ligne suivante. Un mot à lui seul plus large que le
    /// rectangle reste seul sur sa ligne même s'il déborde — le couper romprait le
    /// mot, ce qui serait pire — et le format d'écriture ne le rogne pas : mieux vaut
    /// un débordement visible qu'un mot amputé en silence.
    /// </para>
    /// <para>
    /// Aucun texte n'est tronqué : si aucune taille au-dessus du plancher ne suffit
    /// (en pratique, ça ne devrait jamais arriver), le meilleur découpage possible est
    /// rendu à la taille plancher, quitte à déborder du rectangle.
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class ReecrivainParAjustement : IReecrivainDeTexte
    {
        #region Constantes

        private const float TailleMinimale = 8f;
        private const float PasDeReduction = 2f;

        #endregion

        #region Attributs

        private string nomDePolice;
        private Couleur couleur;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un réécrivain qui écrit en noir avec la police Arial.
        /// </summary>
        public ReecrivainParAjustement()
            : this("Arial", new Couleur(0, 0, 0))
        {
        }

        /// <summary>
        /// Initialise un réécrivain avec la police et la couleur indiquées.
        /// </summary>
        /// <param name="nomDePolice">Le nom de la police à utiliser pour le rendu.</param>
        /// <param name="couleur">La couleur du texte réécrit.</param>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="nomDePolice"/> ou <paramref name="couleur"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="nomDePolice"/> est vide ou ne contient que des espaces.
        /// </exception>
        public ReecrivainParAjustement(string nomDePolice, Couleur couleur)
        {
            if (nomDePolice == null)
            {
                throw new ArgumentNullException(nameof(nomDePolice));
            }

            if (string.IsNullOrWhiteSpace(nomDePolice))
            {
                throw new ArgumentException("Le nom de la police ne peut pas être vide.", nameof(nomDePolice));
            }

            this.nomDePolice = nomDePolice;
            this.couleur = couleur ?? throw new ArgumentNullException(nameof(couleur));
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Le nom de la police utilisée pour le rendu du texte traduit.
        /// </summary>
        public string NomDePolice
        {
            get { return nomDePolice; }
            set { nomDePolice = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// La couleur du texte réécrit.
        /// </summary>
        public Couleur Couleur
        {
            get { return couleur; }
            set { couleur = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        #endregion

        #region Méthodes

        /// <inheritdoc />
        public Planche Reecrire(Planche planche)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            if (planche.Image.Length == 0)
            {
                throw new ArgumentException("L'image de la planche est vide.", nameof(planche));
            }

            using Bitmap composee = Charger(planche.Image);

            float ratioCellule = RatioCellule();

            using (Graphics dessin = Graphics.FromImage(composee))
            {
                dessin.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                dessin.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                foreach (ZoneDeTexte zone in planche.Zones)
                {
                    if (zone.Bulle == null || zone.Bulle.Contour.Count < 3 || string.IsNullOrEmpty(zone.TexteTraduit))
                    {
                        // Sans bulle, il n'y a pas de place connue où écrire. Sans
                        // traduction (nulle ou vide), il n'y a rien à écrire : voir la
                        // remarque de l'interface sur la différence entre les deux.
                        continue;
                    }

                    if (zone.HauteurDeLigne == null)
                    {
                        // Une zone avec bulle devrait toujours porter la hauteur de
                        // ligne mesurée par la lecture : si elle manque, une étape
                        // d'avant a un problème, ce n'est pas à la réécriture de
                        // deviner une taille à sa place.
                        throw new InvalidOperationException(
                            "La zone « " + zone.TexteOriginal + " » a une bulle et une traduction, mais aucune " +
                            "hauteur de ligne mesurée. Elle aurait dû être renseignée à la lecture.");
                    }

                    EcrireDansLeRectangle(
                        dessin, zone.TexteTraduit, zone.HauteurDeLigne.Value, ratioCellule, Boite(zone.Rectangle));
                }
            }

            // Une copie a été dessinée depuis le début : la planche reçue n'a pas été
            // touchée, et l'appelante garde son image nettoyée intacte.
            return planche.AvecImage(Encoder(composee));
        }

        #endregion

        #region Méthodes privées

        private static Bitmap Charger(byte[] image)
        {
            using MemoryStream flux = new MemoryStream(image);

            try
            {
                // GDI+ ne signale pas toujours une image invalide par une
                // ArgumentException : un flux corrompu ressort souvent en
                // ExternalException ou, plus surprenant encore, en
                // OutOfMemoryException — c'est ainsi que GDI+ dit « format inconnu ».
                using Bitmap chargee = new Bitmap(flux);

                return new Bitmap(chargee);
            }
            catch (Exception exception) when (
                exception is ArgumentException or ExternalException or OutOfMemoryException)
            {
                throw new ArgumentException(
                    "Les octets de la planche ne forment pas une image décodable.", nameof(image));
            }
        }

        private static byte[] Encoder(Bitmap image)
        {
            using MemoryStream sortie = new MemoryStream();

            image.Save(sortie, ImageFormat.Png);

            return sortie.ToArray();
        }

        private float RatioCellule()
        {
            // GDI+ prend une taille de police en pixels comme corps (l'em), pas comme
            // hauteur visible. Les métriques déclarées de la police (ascendant +
            // descendant) surestiment largement ce qu'un détecteur mesure sur une
            // ligne réelle : sur la planche d'essai, une hauteur détectée de 45px
            // rendue à la taille que ce ratio implique ne produisait que 28px
            // d'encre — presque 40 % de moins que voulu. On mesure donc directement,
            // en rendant une lettre capitale à une taille de référence et en
            // regardant la hauteur d'encre qu'elle occupe vraiment : c'est ce
            // rapport-là qui reproduit la hauteur d'origine, pas celui des métriques
            // déclarées.
            const float TailleDeReference = 200f;

            using Bitmap etalon = new Bitmap(300, 300);

            using (Graphics dessin = Graphics.FromImage(etalon))
            {
                dessin.Clear(Color.White);
                dessin.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                using Font police = new Font(nomDePolice, TailleDeReference, GraphicsUnit.Pixel);

                dessin.DrawString("H", police, Brushes.Black, PointF.Empty);
            }

            int hauteurEncre = HauteurDeLEncre(etalon);

            return hauteurEncre > 0 ? hauteurEncre / TailleDeReference : 1f;
        }

        private static int HauteurDeLEncre(Bitmap image)
        {
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Color pixel = image.GetPixel(x, y);

                    if (pixel.R < 250 || pixel.G < 250 || pixel.B < 250)
                    {
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            return maxY >= minY ? maxY - minY : 0;
        }

        private static RectangleF Boite(Quadrilatere rectangle)
        {
            // Le rectangle est déjà celui qu'un cadrage en amont a, si possible,
            // maximisé dans le contour de la bulle : ni marge ni rétrécissement
            // supplémentaire ici, ce serait rétrécir deux fois pour rien.
            return new RectangleF(
                (float)rectangle.HautGauche.X,
                (float)rectangle.HautGauche.Y,
                (float)rectangle.Largeur,
                (float)rectangle.Hauteur);
        }

        private void EcrireDansLeRectangle(
            Graphics dessin, string texte, double hauteurDeLigne, float ratioCellule, RectangleF boite)
        {
            float depart = (float)Math.Max(TailleMinimale, hauteurDeLigne / ratioCellule);

            (float taille, List<string> lignes) = TrouverLaTailleQuiTient(dessin, texte, boite, depart);

            using Font police = new Font(nomDePolice, taille, GraphicsUnit.Pixel);
            using SolidBrush pinceau = new SolidBrush(Color.FromArgb(couleur.Rouge, couleur.Vert, couleur.Bleu));

            // Chaque ligne est déjà découpée par DecouperEnLignes ; GDI+ ne doit pas
            // retenter son propre retour à la ligne à l'intérieur du rectangle d'une
            // seule ligne de haut, sans quoi la portion qu'il renverrait à une ligne
            // suivante n'a nulle part où s'afficher et disparaît — c'est NoWrap qui
            // l'en empêche. NoClip s'occupe du rendu, mais pas du choix du texte à
            // afficher : un StringFormat par défaut trime le texte caractère par
            // caractère (Trimming = StringTrimming.Character) pour le faire tenir dans
            // la largeur du rectangle, silencieusement et avant même que NoClip
            // n'entre en jeu — c'est ce qui amputait « commande » en « command ».
            // Trimming = None laisse le mot isolé trop large déborder en entier (voir
            // DecouperEnLignes) plutôt que d'être rogné en silence.
            using StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap
            };

            float hauteurLigne = police.GetHeight(dessin);
            float y = boite.Y + ((boite.Height - (lignes.Count * hauteurLigne)) / 2f);

            foreach (string ligne in lignes)
            {
                dessin.DrawString(
                    ligne, police, pinceau, new RectangleF(boite.X, y, boite.Width, hauteurLigne), format);

                y += hauteurLigne;
            }
        }

        private (float taille, List<string> lignes) TrouverLaTailleQuiTient(
            Graphics dessin, string texte, RectangleF boite, float depart)
        {
            for (float taille = depart; taille > TailleMinimale; taille -= PasDeReduction)
            {
                using Font police = new Font(nomDePolice, taille, GraphicsUnit.Pixel);

                List<string> lignes = DecouperEnLignes(dessin, texte, police, boite.Width);

                if (lignes.Count * police.GetHeight(dessin) <= boite.Height)
                {
                    return (taille, lignes);
                }
            }

            // Aucune taille au-dessus du plancher ne tient : on rend le meilleur
            // découpage possible à la taille minimale plutôt que de tronquer le texte.
            using Font policeMinimale = new Font(nomDePolice, TailleMinimale, GraphicsUnit.Pixel);

            return (TailleMinimale, DecouperEnLignes(dessin, texte, policeMinimale, boite.Width));
        }

        private static List<string> DecouperEnLignes(Graphics dessin, string texte, Font police, float largeurMax)
        {
            string[] mots = texte.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            List<string> lignes = new List<string>();
            string ligneCourante = string.Empty;

            foreach (string mot in mots)
            {
                string essai = ligneCourante.Length == 0 ? mot : ligneCourante + " " + mot;

                if (ligneCourante.Length == 0 || dessin.MeasureString(essai, police).Width <= largeurMax)
                {
                    ligneCourante = essai;
                }
                else
                {
                    lignes.Add(ligneCourante);
                    ligneCourante = mot;
                }
            }

            if (ligneCourante.Length > 0)
            {
                lignes.Add(ligneCourante);
            }

            if (lignes.Count == 0)
            {
                lignes.Add(string.Empty);
            }

            return lignes;
        }

        #endregion
    }
}
