namespace ScanTrad.Application.Common
{
    /// <summary>
    /// Issue d'un cas d'usage : une réussite, ou un échec portant une <see cref="Common.Error"/>.
    /// </summary>
    /// <remarks>
    /// Les échecs prévus (un nom déjà pris, une règle non respectée) passent par ce type ;
    /// les exceptions restent réservées à l'imprévu (base inaccessible, bug).
    /// </remarks>
    public class Result
    {
        #region Attributs

        private readonly Error? error;

        #endregion

        #region Propriétés

        /// <summary>
        /// <c>true</c> si le cas d'usage a réussi.
        /// </summary>
        public bool IsSuccess
        {
            get { return error == null; }
        }

        /// <summary>
        /// <c>true</c> si le cas d'usage a échoué.
        /// </summary>
        public bool IsFailure
        {
            get { return !IsSuccess; }
        }

        /// <summary>
        /// L'erreur qui a fait échouer le cas d'usage.
        /// </summary>
        /// <exception cref="InvalidOperationException">Le résultat est une réussite.</exception>
        public Error Error
        {
            get { return error ?? throw new InvalidOperationException("Un résultat réussi n'a pas d'erreur."); }
        }

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise un résultat. Passer par <see cref="Success"/> ou <see cref="Failure"/>.
        /// </summary>
        /// <param name="error">L'erreur en cas d'échec, <c>null</c> en cas de réussite.</param>
        protected Result(Error? error)
        {
            this.error = error;
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Crée un résultat réussi.
        /// </summary>
        /// <returns>Le résultat réussi.</returns>
        public static Result Success()
        {
            return new Result(null);
        }

        /// <summary>
        /// Crée un résultat en échec.
        /// </summary>
        /// <param name="error">L'erreur qui a fait échouer le cas d'usage.</param>
        /// <returns>Le résultat en échec.</returns>
        public static Result Failure(Error error)
        {
            return new Result(error);
        }

        /// <summary>
        /// Convertit une erreur en résultat en échec, pour pouvoir écrire
        /// <c>return UserErrors.UsernameTaken;</c> dans un cas d'usage.
        /// </summary>
        /// <param name="error">L'erreur qui a fait échouer le cas d'usage.</param>
        public static implicit operator Result(Error error)
        {
            return Failure(error);
        }

        #endregion
    }
}
