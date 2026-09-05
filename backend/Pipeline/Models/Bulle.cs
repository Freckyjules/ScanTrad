namespace ScanTrad.Pipeline.Models
{
    /// <summary>
    /// Le contour d'une bulle de dialogue, décrit par les points qui en suivent le
    /// pourtour. C'est la zone qu'on a le droit d'effacer, et dans laquelle le texte
    /// traduit devra tenir.
    /// </summary>
    /// <remarks>
    /// Le contour est un polygone et non une image en noir et blanc, pour une raison
    /// pratique : l'utilisateur doit pouvoir rattraper une détection approximative en
    /// déplaçant les points à la souris depuis le front. Sur un masque en pixels, il
    /// n'y aurait rien à attraper.
    /// </remarks>
    public class Bulle
    {
        #region Attributs

        private List<Coordonnee> contour;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise une bulle sans contour, à remplir point par point.
        /// </summary>
        public Bulle()
        {
            contour = new List<Coordonnee>();
        }

        /// <summary>
        /// Initialise une bulle à partir des points de son contour, dans l'ordre où
        /// ils se suivent le long du pourtour. Le dernier point rejoint le premier :
        /// il n'y a pas à le répéter.
        /// </summary>
        /// <param name="points">Les points du contour, au moins trois.</param>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="points"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="points"/> contient moins de trois points, un
        /// polygone n'ayant pas de sens en deçà.
        /// </exception>
        public Bulle(IEnumerable<Coordonnee> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            contour = new List<Coordonnee>(points);

            if (contour.Count < 3)
            {
                throw new ArgumentException("Un contour de bulle demande au moins trois points.", nameof(points));
            }
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Les points du contour, dans l'ordre du pourtour. La liste se modifie —
        /// c'est ainsi que l'utilisateur corrige une bulle mal détectée — mais elle
        /// ne peut pas être remplacée : la bulle reste propriétaire de son contour.
        /// </summary>
        public IList<Coordonnee> Contour
        {
            get { return contour; }
        }

        /// <summary>
        /// Le centre géométrique du contour, ou l'origine de l'image si le contour
        /// est encore vide.
        /// </summary>
        public Coordonnee Centre
        {
            get
            {
                if (contour.Count == 0)
                {
                    return new Coordonnee();
                }

                double sommeX = 0;
                double sommeY = 0;

                foreach (Coordonnee point in contour)
                {
                    sommeX += point.X;
                    sommeY += point.Y;
                }

                return new Coordonnee(sommeX / contour.Count, sommeY / contour.Count);
            }
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Indique si un point se trouve à l'intérieur de la bulle.
        /// </summary>
        /// <remarks>
        /// Sert à rattacher une zone de texte à sa bulle — le centre du texte
        /// tombe-t-il dedans ? — et à vérifier au rendu qu'une ligne de français ne
        /// déborde pas. Un point posé exactement sur le contour peut être vu dedans
        /// ou dehors : ne comptez pas dessus pour un cas limite.
        /// </remarks>
        /// <param name="point">Le point à situer.</param>
        /// <returns>
        /// <c>true</c> si le point est à l'intérieur du contour, <c>false</c> sinon
        /// ou si le contour compte moins de trois points.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="point"/> vaut <c>null</c>.
        /// </exception>
        public bool Contient(Coordonnee point)
        {
            if (point == null)
            {
                throw new ArgumentNullException(nameof(point));
            }

            bool dedans = false;

            for (int i = 0, j = contour.Count - 1; i < contour.Count; j = i++)
            {
                Coordonnee courant = contour[i];
                Coordonnee precedent = contour[j];

                bool encadreLeNiveau = (courant.Y > point.Y) != (precedent.Y > point.Y);

                if (encadreLeNiveau)
                {
                    double abscisseTraversee = courant.X
                        + ((point.Y - courant.Y) * (precedent.X - courant.X) / (precedent.Y - courant.Y));

                    if (point.X < abscisseTraversee)
                    {
                        dedans = !dedans;
                    }
                }
            }

            return dedans;
        }

        /// <summary>
        /// Décrit la bulle par la taille de son contour et sa position.
        /// </summary>
        /// <returns>Une description lisible de la bulle.</returns>
        public override string ToString()
        {
            return FormattableString.Invariant($"contour de {contour.Count} points, centré en {Centre}");
        }

        #endregion
    }
}
