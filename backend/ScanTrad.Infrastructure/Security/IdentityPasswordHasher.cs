using Microsoft.AspNetCore.Identity;
using ScanTrad.Application.Interfaces;

namespace ScanTrad.Infrastructure.Security
{
    /// <summary>
    /// Implémentation du service de hachage des mots de passe.
    /// </summary>
    public class IdentityPasswordHasher : IPasswordHasher
    {
        #region Attributs

        private readonly PasswordHasher<object> inner;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise le service sur le hacheur d'ASP.NET Core Identity.
        /// </summary>
        public IdentityPasswordHasher()
        {
            inner = new PasswordHasher<object>();
        }

        #endregion

        #region Méthodes

        /// <inheritdoc />
        public string HashPassword(string password)
        {
            return inner.HashPassword(user: null!, password);
        }

        #endregion
    }
}
