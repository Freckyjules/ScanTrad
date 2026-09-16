using OpenCvSharp;
using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Cadrage
{
    /// <summary>
    /// Maximise le rectangle d'une zone en rasterisant sa bulle en masque binaire, puis
    /// en y cherchant le plus grand rectangle qui tient entièrement dans les pixels
    /// allumés.
    /// </summary>
    /// <remarks>
    /// Le contour d'une bulle n'est pas forcément convexe, et rien ne garantit qu'il le
    /// reste après une correction manuelle depuis le front. Une formule fermée pour le
    /// plus grand rectangle inscrit dans un polygone quelconque est délicate à écrire
    /// et à faire confiance ; la rasterisation, elle, n'a pas ce problème — un pixel est
    /// dedans ou dehors, sans cas particulier à traiter.
    /// <para>
    /// Le masque est rempli sans anti-crénelage : un pixel de bord partiellement
    /// couvert doit compter comme dehors, pas comme dedans, sans quoi le rectangle
    /// trouvé pourrait mordre le contour d'une fraction de pixel.
    /// </para>
    /// <para>
    /// Le plus grand rectangle du masque se trouve ensuite par l'algorithme classique
    /// du plus grand rectangle dans un histogramme, ligne par ligne : pour chaque ligne
    /// du masque, la hauteur de pixels allumés qui s'empilent verticalement au-dessus
    /// de chaque colonne forme un histogramme, et son plus grand rectangle se calcule en
    /// temps linéaire avec une pile. Répété sur chaque ligne, ça trouve le plus grand
    /// rectangle du masque entier en temps linéaire dans le nombre de pixels — exact,
    /// à la résolution du pixel près, et non une approximation.
    /// </para>
    /// <para>
    /// Une bulle trop fine pour contenir le moindre pixel entier — en pratique, ça ne
    /// devrait pas arriver — laisse le rectangle d'origine inchangé plutôt que d'écrire
    /// un rectangle de surface nulle.
    /// </para>
    /// </remarks>
    public class AjusteurDeRectangleParRasterisation : IAjusteurDeRectangle
    {
        #region Méthodes

        /// <inheritdoc />
        public Planche Ajuster(Planche planche)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            foreach (ZoneDeTexte zone in planche.Zones)
            {
                if (zone.Bulle == null || zone.Bulle.Contour.Count < 3)
                {
                    // Sans bulle, il n'y a rien à maximiser : voir la remarque de
                    // l'interface.
                    continue;
                }

                Quadrilatere? maximise = RectangleMaximal(zone.Bulle);

                if (maximise != null)
                {
                    zone.Rectangle = maximise;
                }
            }

            return planche.AvecZones(planche.Zones);
        }

        #endregion

        #region Méthodes privées

        private static Quadrilatere? RectangleMaximal(Bulle bulle)
        {
            double minX = bulle.Contour.Min(point => point.X);
            double maxX = bulle.Contour.Max(point => point.X);
            double minY = bulle.Contour.Min(point => point.Y);
            double maxY = bulle.Contour.Max(point => point.Y);

            int largeurMasque = (int)Math.Ceiling(maxX - minX);
            int hauteurMasque = (int)Math.Ceiling(maxY - minY);

            if (largeurMasque < 1 || hauteurMasque < 1)
            {
                return null;
            }

            using Mat masque = new Mat(hauteurMasque, largeurMasque, MatType.CV_8UC1, Scalar.All(0));

            Point[] contour = bulle.Contour
                .Select(point => new Point(
                    (int)Math.Round(point.X - minX), (int)Math.Round(point.Y - minY)))
                .ToArray();

            // Ni anti-crénelage ni connectivité 4 : un remplissage plein et net, pour
            // qu'un pixel du masque ne veuille dire qu'une seule chose.
            Cv2.FillPoly(masque, new[] { contour }, Scalar.All(255));

            (int gauche, int haut, int largeur, int hauteur) = PlusGrandRectangleDuMasque(masque);

            if (largeur < 1 || hauteur < 1)
            {
                return null;
            }

            return Quadrilatere.DepuisRectangle(minX + gauche, minY + haut, largeur, hauteur);
        }

        private static (int gauche, int haut, int largeur, int hauteur) PlusGrandRectangleDuMasque(Mat masque)
        {
            int[] histogramme = new int[masque.Cols];

            int meilleureSurface = 0;
            int meilleurGauche = 0;
            int meilleurHaut = 0;
            int meilleureLargeur = 0;
            int meilleureHauteur = 0;

            for (int y = 0; y < masque.Rows; y++)
            {
                for (int x = 0; x < masque.Cols; x++)
                {
                    histogramme[x] = masque.At<byte>(y, x) != 0 ? histogramme[x] + 1 : 0;
                }

                (int surface, int gauche, int largeur, int hauteur) = PlusGrandRectangleDeLHistogramme(histogramme);

                if (surface > meilleureSurface)
                {
                    meilleureSurface = surface;
                    meilleurGauche = gauche;
                    meilleurHaut = y - hauteur + 1;
                    meilleureLargeur = largeur;
                    meilleureHauteur = hauteur;
                }
            }

            return (meilleurGauche, meilleurHaut, meilleureLargeur, meilleureHauteur);
        }

        private static (int surface, int gauche, int largeur, int hauteur) PlusGrandRectangleDeLHistogramme(
            int[] histogramme)
        {
            // Pile des index dont la barre n'a encore trouvé personne de plus bas
            // qu'elle à sa droite ; tant que c'est le cas, on ne sait pas encore
            // jusqu'où sa largeur s'étend.
            Stack<int> pile = new Stack<int>();

            int meilleureSurface = 0;
            int meilleurGauche = 0;
            int meilleureLargeur = 0;
            int meilleureHauteur = 0;

            for (int i = 0; i <= histogramme.Length; i++)
            {
                int hauteurCourante = i < histogramme.Length ? histogramme[i] : 0;

                while (pile.Count > 0 && histogramme[pile.Peek()] >= hauteurCourante)
                {
                    int hauteurBarre = histogramme[pile.Pop()];
                    int gauche = pile.Count == 0 ? 0 : pile.Peek() + 1;
                    int largeur = i - gauche;
                    int surface = hauteurBarre * largeur;

                    if (surface > meilleureSurface)
                    {
                        meilleureSurface = surface;
                        meilleurGauche = gauche;
                        meilleureLargeur = largeur;
                        meilleureHauteur = hauteurBarre;
                    }
                }

                pile.Push(i);
            }

            return (meilleureSurface, meilleurGauche, meilleureLargeur, meilleureHauteur);
        }

        #endregion
    }
}
