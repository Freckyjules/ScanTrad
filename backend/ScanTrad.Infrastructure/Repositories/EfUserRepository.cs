using Microsoft.EntityFrameworkCore;
using ScanTrad.Domain.Entities;
using ScanTrad.Domain.Repositories;
using ScanTrad.Infrastructure.Data;

namespace ScanTrad.Infrastructure.Repositories
{
    /// <summary>
    /// Implémentation du repository pour les utilisateurs utilisant Entity Framework <see cref="IUserRepository"/>.
    /// </summary>
    public class EfUserRepository : IUserRepository
    {
        #region Attributs

        private readonly ScanTradDbContext context;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise le repository sur le contexte EF Core de l'application.
        /// </summary>
        /// <param name="context">Le contexte de la base ScanTrad.</param>
        public EfUserRepository(ScanTradDbContext context)
        {
            this.context = context;
        }

        #endregion

        #region Méthodes

        /// <inheritdoc/>
        /// <exception cref="DbUpdateException">
        /// L'enregistrement a échoué, notamment si le nom d'utilisateur est déjà pris.
        /// </exception>
        public async Task AddAsync(User user)
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> ExistsByUsernameAsync(string username)
        {
            // Normalisé ici, en C#, une seule fois : la base compare alors directement
            // avec l'index, sans convertir chaque ligne.
            string normalizedUsername = User.NormalizeUsername(username);

            return await context.Users.AnyAsync(u => u.NormalizedUsername == normalizedUsername);
        }

        #endregion
    }
}
