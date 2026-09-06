using Microsoft.ML.OnnxRuntime.Tensors;

namespace ScanTrad.Pipeline.Traduction.Moteurs
{
    /// <summary>
    /// Une hypothèse de traduction en cours de construction, dans la recherche en
    /// faisceau : les jetons produits jusqu'ici, ce qu'ils valent, et le cache du
    /// décodeur qui va avec.
    /// </summary>
    /// <remarks>
    /// Chaque faisceau porte <b>son propre cache</b>. Deux hypothèses qui divergent au
    /// troisième jeton n'ont pas la même histoire, donc pas le même état interne du
    /// décodeur — les mélanger produirait un texte incohérent.
    /// <para>
    /// Les tenseurs du cache ne sont jamais modifiés sur place : à chaque tour un
    /// faisceau reçoit un tableau neuf. Deux faisceaux issus du même parent peuvent
    /// donc partager les tenseurs de ce parent sans risque.
    /// </para>
    /// </remarks>
    public class FaisceauDeDecodage
    {
        #region Attributs

        private List<int> jetons;
        private double score;
        private DenseTensor<float>[] cache;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Crée un faisceau à partir de son historique, de son score et de son cache.
        /// </summary>
        /// <param name="jetons">Les jetons produits jusqu'ici.</param>
        /// <param name="score">La somme des logarithmes de vraisemblance.</param>
        /// <param name="cache">Le cache du décodeur correspondant à cet historique.</param>
        public FaisceauDeDecodage(List<int> jetons, double score, DenseTensor<float>[] cache)
        {
            this.jetons = jetons;
            this.score = score;
            this.cache = cache;
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Les jetons produits jusqu'ici, dans l'ordre.
        /// </summary>
        public List<int> Jetons
        {
            get { return jetons; }
        }

        /// <summary>
        /// La somme des logarithmes de vraisemblance des jetons produits. On additionne
        /// des logarithmes plutôt que de multiplier des probabilités : le produit de
        /// vingt nombres inférieurs à un tomberait sous la précision du flottant.
        /// </summary>
        public double Score
        {
            get { return score; }
        }

        /// <summary>
        /// Le cache du décodeur pour cet historique.
        /// </summary>
        public DenseTensor<float>[] Cache
        {
            get { return cache; }
        }

        /// <summary>
        /// Le dernier jeton produit, celui qu'il faut réinjecter au tour suivant.
        /// </summary>
        public int Dernier
        {
            get { return jetons[jetons.Count - 1]; }
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Calcule le score rapporté à la longueur de l'hypothèse.
        /// </summary>
        /// <remarks>
        /// Sans cette normalisation, une hypothèse courte gagnerait toujours : chaque
        /// jeton ajoute un logarithme négatif, donc une phrase longue est
        /// mécaniquement moins bien notée qu'une phrase tronquée qui dit la moitié.
        /// </remarks>
        /// <returns>Le score moyen par jeton.</returns>
        public double ScoreNormalise()
        {
            return jetons.Count == 0 ? score : score / jetons.Count;
        }

        #endregion
    }
}
