using ScanTrad.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ScanTrad.Infrastructure.Security
{
    /// <summary>
    /// Implémentation du service de hachage des mots de passe.
    /// </summary>
    public class IdentityPasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher<object> inner = new();

        /// <inheritdoc />
        public string HashPassword(string password)
        {
            return inner.HashPassword(user: null!, password);
        }
    }
}
