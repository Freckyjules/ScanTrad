using OpenCvSharp;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace ScanTrad.Pipeline.Lecture
{
    /// <summary>
    /// Lit une planche en combinant trois outils locaux : comic-text-detector pour
    /// situer les blocs de dialogue, PaddleOCR pour les lire, et OpenCV pour
    /// retrouver le contour des bulles.
    /// </summary>
    /// <remarks>
    /// Le modèle manga donne des <b>blocs</b>, déjà groupés par bulle et débarrassés
    /// des onomatopées. Chaque bloc est découpé dans la planche et lu séparément, ce
    /// qui a deux conséquences mesurées :
    /// <list type="bullet">
    /// <item>il n'y a plus rien à regrouper, donc plus de seuils géométriques à
    /// régler ni de cas tordus à rattraper ;</item>
    /// <item>la reconnaissance travaille sur une découpe de deux à trois cents
    /// pixels, donc à pleine résolution, au lieu de la planche entière réduite. Sur
    /// une planche d'essai, les erreurs sont passées d'une dizaine à quatre.</item>
    /// </list>
    /// <para>
    /// La boîte du bloc sert directement de quadrilatère : on ne cherche pas à
    /// remonter les coordonnées des lignes trouvées dans la découpe.
    /// </para>
    /// <para>
    /// Une dernière passe assemble les zones qui ont abouti dans la même bulle. Le
    /// chercheur travaille bloc par bloc et ne voit pas que la diffusion a déjà
    /// parcouru cette région : deux lobes d'une bulle liée, dont le blanc intérieur
    /// communique, rendent deux fois le même contour au point près. Ce n'est pas du
    /// regroupement par proximité — le critère est une égalité exacte, sans seuil —
    /// mais la réparation d'une duplication née ici. Les deux fragments portent de
    /// toute façon une seule phrase, que la traduction doit voir d'un tenant.
    /// </para>
    /// <para>
    /// Le prix de cette approche est un échange précision contre rappel : ce que le
    /// modèle rate est définitivement perdu, là où une détection ligne par ligne
    /// ramasse tout, bruit compris.
    /// </para>
    /// </remarks>
    public class LecteurDePlanche : ILecteurDePlanche
    {
        #region Constantes

        /// <summary>
        /// Marge ajoutée autour d'un bloc avant de le découper. La boîte du modèle
        /// colle au texte, et sans marge le dernier caractère se fait rogner.
        /// </summary>
        private const int MargeDeDecoupe = 12;

        #endregion

        #region Attributs

        private DetecteurDeBlocs detecteur;
        private PaddleOcrAll moteurDeLecture;
        private double confianceMinimale;
        private bool libere;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un lecteur à partir du fichier du modèle, avec un seuil de
        /// confiance courant.
        /// </summary>
        /// <param name="cheminDuModele">
        /// Chemin du fichier <c>comictextdetector.onnx</c>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="cheminDuModele"/> est vide.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// Levée si le fichier du modèle est introuvable.
        /// </exception>
        public LecteurDePlanche(string cheminDuModele)
            : this(cheminDuModele, 0.5)
        {
        }

        /// <summary>
        /// Initialise un lecteur avec un seuil de confiance choisi.
        /// </summary>
        /// <param name="cheminDuModele">
        /// Chemin du fichier <c>comictextdetector.onnx</c>.
        /// </param>
        /// <param name="confianceMinimale">
        /// En dessous de ce seuil, un bloc lu est jeté sans être remonté.
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
        public LecteurDePlanche(string cheminDuModele, double confianceMinimale)
        {
            if (confianceMinimale < 0 || confianceMinimale > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(confianceMinimale), "Le seuil de confiance se situe entre 0 et 1.");
            }

            this.confianceMinimale = confianceMinimale;
            this.libere = false;
            this.detecteur = new DetecteurDeBlocs(cheminDuModele);

            this.moteurDeLecture = new PaddleOcrAll(LocalFullModels.EnglishV5)
            {
                // Le détecteur de PaddleOCR exprime beaucoup de lignes horizontales
                // comme des rectangles pivotés d'un quart de tour. Le laisser
                // redresser ses découpes sur cette base lui fait lire du texte
                // couché : « MAGIC ACADEMY » ressortait en « WAMIC ACADEAA ».
                AllowRotateDetection = false,

                // Le classifieur d'orientation à 180° fait planter le moteur natif.
                // Une planche de manga n'est de toute façon pas à l'envers.
                Enable180Classification = false
            };
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Seuil en dessous duquel un bloc lu est jeté, entre 0 et 1.
        /// </summary>
        public double ConfianceMinimale
        {
            get { return confianceMinimale; }
            set { confianceMinimale = value; }
        }

        #endregion

        #region Méthodes

        /// <inheritdoc />
        public Task<Planche> LireAsync(Planche planche, CancellationToken jetonAnnulation = default)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            if (planche.Image.Length == 0)
            {
                throw new ArgumentException("L'image de la planche est vide.", nameof(planche));
            }

            ObjectDisposedException.ThrowIf(libere, this);

            return Task.Run(() => planche.AvecZones(Lire(planche.Image, jetonAnnulation)), jetonAnnulation);
        }

        /// <summary>
        /// Libère le modèle de détection et le moteur de lecture retenus en mémoire.
        /// </summary>
        public void Dispose()
        {
            if (libere)
            {
                return;
            }

            detecteur.Dispose();
            moteurDeLecture.Dispose();
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

            IReadOnlyList<Rect> blocs = detecteur.Detecter(planche);

            jetonAnnulation.ThrowIfCancellationRequested();

            using Mat gris = new Mat();
            Cv2.CvtColor(planche, gris, ColorConversionCodes.BGR2GRAY);

            List<ZoneDeTexte> zones = new List<ZoneDeTexte>();

            foreach (Rect bloc in blocs)
            {
                jetonAnnulation.ThrowIfCancellationRequested();

                ZoneDeTexte? zone = ConstruireLaZone(planche, gris, bloc);

                if (zone != null)
                {
                    zones.Add(zone);
                }
            }

            return Assembler(zones);
        }

        private static List<ZoneDeTexte> Assembler(List<ZoneDeTexte> zones)
        {
            // Deux blocs peuvent tomber dans la même bulle : le chercheur travaille
            // bloc par bloc et ne voit pas que la diffusion a déjà parcouru cette
            // région. Il rend alors deux fois le même contour, au point près.
            List<ZoneDeTexte> assemblees = new List<ZoneDeTexte>();
            List<ZoneDeTexte> restantes = new List<ZoneDeTexte>(zones);

            while (restantes.Count > 0)
            {
                List<ZoneDeTexte> fragments = restantes
                    .Where(zone => MemeBulle(zone, restantes[0]))
                    .ToList();

                restantes.RemoveAll(zone => fragments.Contains(zone));

                assemblees.Add(fragments.Count == 1 ? fragments[0] : Fusionner(fragments));
            }

            return assemblees;
        }

        private static ZoneDeTexte Fusionner(IReadOnlyList<ZoneDeTexte> fragments)
        {
            // Les fragments arrivent dans l'ordre du détecteur, qui rend ses blocs de
            // haut en bas : c'est déjà le bon ordre pour deux lobes empilés, le cas
            // courant d'une bulle liée. Deux lobes côte à côte se recolleraient dans
            // un ordre arbitraire ; on n'en a pas rencontré, et deviner demanderait le
            // sens de lecture, que le lecteur n'a pas à connaître.
            ZoneDeTexte assemblee = new ZoneDeTexte();

            assemblee.Rectangle = Englober(fragments.Select(fragment => fragment.Rectangle));
            assemblee.Angle = AngleMoyen(fragments.Select(fragment => fragment.Angle));
            assemblee.HauteurDeLigne = HauteurMoyenne(fragments);
            assemblee.Bulle = fragments[0].Bulle;
            assemblee.CouleurDeFond = fragments[0].CouleurDeFond;

            // Recoller avec une espace, comme les lignes d'un même bloc : les deux
            // lobes d'une bulle liée portent une seule phrase, et la traduire d'un
            // seul tenant est le vrai gain de l'assemblage.
            assemblee.TexteOriginal = string.Join(
                " ", fragments.Select(fragment => fragment.TexteOriginal.Trim()));

            // Un assemblage ne vaut que ce que vaut son fragment le moins sûr.
            assemblee.Confiance = fragments.Min(fragment => fragment.Confiance);

            return assemblee;
        }

        private static bool MemeBulle(ZoneDeTexte une, ZoneDeTexte autre)
        {
            if (ReferenceEquals(une, autre))
            {
                return true;
            }

            // Deux zones sans bulle ne se ressemblent pas pour autant : une onomatopée
            // n'a rien à voir avec une autre.
            if (une.Bulle == null || autre.Bulle == null
                || une.Bulle.Contour.Count != autre.Bulle.Contour.Count)
            {
                return false;
            }

            return !une.Bulle.Contour
                .Where((point, rang) =>
                    point.X != autre.Bulle.Contour[rang].X || point.Y != autre.Bulle.Contour[rang].Y)
                .Any();
        }

        private static Quadrilatere Englober(IEnumerable<Quadrilatere> rectangles)
        {
            List<Coordonnee> coins = rectangles
                .SelectMany(rectangle => new[]
                {
                    rectangle.HautGauche, rectangle.HautDroit, rectangle.BasDroit, rectangle.BasGauche
                })
                .ToList();

            double gauche = coins.Min(coin => coin.X);
            double haut = coins.Min(coin => coin.Y);

            return Quadrilatere.DepuisRectangle(
                gauche, haut, coins.Max(coin => coin.X) - gauche, coins.Max(coin => coin.Y) - haut);
        }

        private static double? HauteurMoyenne(IReadOnlyList<ZoneDeTexte> fragments)
        {
            double[] mesurees = fragments
                .Where(fragment => fragment.HauteurDeLigne != null)
                .Select(fragment => fragment.HauteurDeLigne!.Value)
                .ToArray();

            return mesurees.Length == 0 ? null : mesurees.Average();
        }

        private ZoneDeTexte? ConstruireLaZone(Mat planche, Mat gris, Rect bloc)
        {
            Rect decoupe = Elargir(bloc, planche.Size(), MargeDeDecoupe);

            using Mat morceau = new Mat(planche, decoupe);

            PaddleOcrResult resultat = moteurDeLecture.Run(morceau);

            PaddleOcrResultRegion[] lignes = resultat.Regions
                .Where(region => !string.IsNullOrWhiteSpace(region.Text))
                .OrderBy(region => region.Rect.Center.Y)
                .ThenBy(region => region.Rect.Center.X)
                .ToArray();

            if (lignes.Length == 0)
            {
                return null;
            }

            // Un bloc ne vaut que ce que vaut sa ligne la moins sûre.
            double confiance = lignes.Min(ligne => ligne.Score);

            if (confiance < confianceMinimale)
            {
                return null;
            }

            ZoneDeTexte zone = new ZoneDeTexte();

            zone.Rectangle = Quadrilatere.DepuisRectangle(bloc.X, bloc.Y, bloc.Width, bloc.Height);

            // Le modèle rend un rectangle droit : l'inclinaison du texte ne s'y lit
            // pas. Elle se mesure sur les boîtes des lignes, qui elles suivent le
            // texte. Un angle étant invariant par translation, il n'y a pas besoin de
            // ramener ces boîtes dans le repère de la planche.
            Quadrilatere[] formesDesLignes = lignes
                .Select(ligne => VersQuadrilatere(ligne.Rect))
                .ToArray();

            zone.Angle = AngleMoyen(formesDesLignes.Select(forme => forme.Angle));
            zone.HauteurDeLigne = formesDesLignes.Average(forme => forme.Hauteur);

            // Les lignes d'un bloc sont les morceaux d'une même phrase : on les
            // recolle avec une espace, pas un retour à la ligne. Le rendu français
            // recoupera lui-même, et pas au même endroit.
            zone.TexteOriginal = string.Join(" ", lignes.Select(ligne => ligne.Text.Trim()));
            zone.Confiance = confiance;

            // La bulle et la couleur de son papier se trouvent d'un même geste : la
            // couleur se lit sur le masque de la diffusion, qui ne survit pas à
            // l'appel.
            zone.Bulle = ChercheurDeBulle.Chercher(gris, planche, bloc, out Couleur? fond);
            zone.CouleurDeFond = fond;

            // Le texte traduit et l'ordre de lecture relèvent d'étapes ultérieures :
            // le lecteur ne doit surtout pas y toucher.
            return zone;
        }

        private static Quadrilatere VersQuadrilatere(RotatedRect rectangle)
        {
            // On ne se fie pas à l'angle que porte le RotatedRect : le détecteur
            // exprime souvent une ligne horizontale comme un rectangle pivoté d'un
            // quart de tour, et 277x39 à 0° décrit la même chose que 39x277 à -90°.
            // On replace donc les coins par position : les deux plus hauts forment le
            // côté haut, et le plus à gauche de chaque paire vient en premier.
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

        private static double AngleMoyen(IEnumerable<double> angles)
        {
            // On additionne les directions plutôt que les angles : la moyenne
            // arithmétique de 179° et -179° donnerait 0°, alors que la bonne réponse
            // est 180°.
            double sommeX = 0;
            double sommeY = 0;

            foreach (double angle in angles)
            {
                double radians = angle * Math.PI / 180;

                sommeX += Math.Cos(radians);
                sommeY += Math.Sin(radians);
            }

            return Math.Atan2(sommeY, sommeX) * 180 / Math.PI;
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
