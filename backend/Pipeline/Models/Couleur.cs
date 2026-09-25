namespace ScanTrad.Pipeline.Models
{
    /// <summary>
    /// Une couleur opaque, décrite par ses trois composantes de 0 à 255.
    /// </summary>
    /// <remarks>
    /// Le modèle reste volontairement libre de toute bibliothèque d'image : c'est la
    /// même raison qui fait exister <see cref="Coordonnee"/> plutôt qu'un point
    /// d'OpenCV. Les composantes sont nommées dans l'ordre où on les lit à voix
    /// haute, et non dans celui où OpenCV les range en mémoire — la conversion est le
    /// travail de la couche qui manipule des images.
    /// </remarks>
    public class Couleur
    {
        #region Constantes

        private const int Minimum = 0;
        private const int Maximum = 255;

        #endregion

        #region Attributs

        private int rouge;
        private int vert;
        private int bleu;

        #endregion

        #region Propriétés

        /// <summary>
        /// Composante rouge, de 0 à 255.
        /// </summary>
        public int Rouge
        {
            get { return rouge; }
            set { rouge = value; }
        }

        /// <summary>
        /// Composante verte, de 0 à 255.
        /// </summary>
        public int Vert
        {
            get { return vert; }
            set { vert = value; }
        }

        /// <summary>
        /// Composante bleue, de 0 à 255.
        /// </summary>
        public int Bleu
        {
            get { return bleu; }
            set { bleu = value; }
        }

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un noir opaque.
        /// </summary>
        public Couleur()
        {
            rouge = 0;
            vert = 0;
            bleu = 0;
        }

        /// <summary>
        /// Initialise une couleur à partir de ses trois composantes.
        /// </summary>
        /// <param name="rouge">Composante rouge, de 0 à 255.</param>
        /// <param name="vert">Composante verte, de 0 à 255.</param>
        /// <param name="bleu">Composante bleue, de 0 à 255.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Levée si l'une des composantes sort de l'intervalle 0-255.
        /// </exception>
        public Couleur(int rouge, int vert, int bleu)
        {
            Verifier(rouge, nameof(rouge));
            Verifier(vert, nameof(vert));
            Verifier(bleu, nameof(bleu));

            this.rouge = rouge;
            this.vert = vert;
            this.bleu = bleu;
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Décrit la couleur par ses composantes puis par son code hexadécimal, la
        /// forme sous laquelle le front la manipulera.
        /// </summary>
        /// <returns>Une description lisible de la couleur.</returns>
        public override string ToString()
        {
            return FormattableString.Invariant($"RVB({rouge}, {vert}, {bleu}) #{rouge:X2}{vert:X2}{bleu:X2}");
        }

        #endregion

        #region Méthodes privées

        private static void Verifier(int composante, string nom)
        {
            if (composante < Minimum || composante > Maximum)
            {
                throw new ArgumentOutOfRangeException(
                    nom, composante, "Une composante de couleur se situe entre 0 et 255.");
            }
        }

        #endregion
    }
}
