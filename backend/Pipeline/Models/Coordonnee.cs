namespace ScanTrad.Pipeline.Models
{
    /// <summary>
    /// Une coordonnée dans l'image, en pixels. L'origine est le coin haut gauche,
    /// et l'axe vertical descend vers le bas — c'est la convention de toutes les
    /// bibliothèques d'image, et l'inverse des mathématiques.
    /// </summary>
    public class Coordonnee
    {
        #region Attributs

        private double x;
        private double y;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un point placé à l'origine de l'image.
        /// </summary>
        public Coordonnee()
        {
            x = 0;
            y = 0;
        }

        /// <summary>
        /// Initialise un point aux coordonnées indiquées.
        /// </summary>
        /// <param name="x">Abscisse en pixels, depuis le bord gauche.</param>
        /// <param name="y">Ordonnée en pixels, depuis le bord haut.</param>
        public Coordonnee(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Abscisse en pixels, mesurée depuis le bord gauche de l'image.
        /// </summary>
        public double X
        {
            get { return x; }
            set { x = value; }
        }

        /// <summary>
        /// Ordonnée en pixels, mesurée depuis le bord haut de l'image.
        /// </summary>
        public double Y
        {
            get { return y; }
            set { y = value; }
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Calcule la distance en ligne droite entre ce point et un autre.
        /// </summary>
        /// <param name="autre">Le point vers lequel mesurer.</param>
        /// <returns>La distance en pixels.</returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="autre"/> vaut <c>null</c>.
        /// </exception>
        public double DistanceVers(Coordonnee autre)
        {
            if (autre == null)
            {
                throw new ArgumentNullException(nameof(autre));
            }

            double ecartX = autre.X - x;
            double ecartY = autre.Y - y;

            return Math.Sqrt((ecartX * ecartX) + (ecartY * ecartY));
        }

        /// <summary>
        /// Décrit la coordonnée sous la forme « (x, y) ». Les nombres sont écrits
        /// avec un point décimal quelle que soit la machine, pour que les journaux
        /// restent comparables.
        /// </summary>
        /// <returns>Une description lisible de la coordonnée.</returns>
        public override string ToString()
        {
            return FormattableString.Invariant($"({x:0.#}, {y:0.#})");
        }

        #endregion
    }
}
