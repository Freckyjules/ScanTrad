namespace ScanTrad.Application.Common
{
    /// <summary>
    /// Erreur métier prévue, rendue par un cas d'usage à la place d'une exception.
    /// </summary>
    public sealed class Error
    {
        #region Propriétés

        /// <summary>
        /// Identifiant stable de l'erreur, par exemple <c>User.UsernameTaken</c>.
        /// Contrairement au message, il ne change jamais : le front et les tests
        /// peuvent s'appuyer dessus.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Message destiné à l'utilisateur. Peut être reformulé à tout moment.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Famille de l'erreur.
        /// </summary>
        public ErrorType Type { get; }

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise une erreur. Passer par <see cref="Validation"/> ou
        /// <see cref="Conflict"/>, qui fixent la famille.
        /// </summary>
        /// <param name="code">Identifiant stable de l'erreur, destiné aux programmes.</param>
        /// <param name="message">Message destiné à l'utilisateur.</param>
        /// <param name="type">Famille de l'erreur.</param>
        private Error(string code, string message, ErrorType type)
        {
            Code = code;
            Message = message;
            Type = type;
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Crée une erreur de validation : une règle n'est pas respectée.
        /// </summary>
        /// <param name="code">Identifiant stable de l'erreur.</param>
        /// <param name="message">Message destiné à l'utilisateur.</param>
        /// <returns>L'erreur créée.</returns>
        public static Error Validation(string code, string message)
        {
            return new Error(code, message, ErrorType.Validation);
        }

        /// <summary>
        /// Crée une erreur de conflit : la ressource existe déjà.
        /// </summary>
        /// <param name="code">Identifiant stable de l'erreur.</param>
        /// <param name="message">Message destiné à l'utilisateur.</param>
        /// <returns>L'erreur créée.</returns>
        public static Error Conflict(string code, string message)
        {
            return new Error(code, message, ErrorType.Conflict);
        }

        #endregion
    }
}
