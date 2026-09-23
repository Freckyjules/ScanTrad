using ScanTrad.Domain.Entities;
using ScanTrad.Domain.Repositories;
using ScanTrad.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScanTrad.Infrastructure.Repositories
{
    /// <summary>
    /// Implémentation du repository pour les utilisateurs utilisant Entity Framework <see cref="IUserRepository"/>.
    /// </summary>
    public class EfUserRepository : IUserRepository
    {
        private readonly ScanTradDbContext context;

        public EfUserRepository(ScanTradDbContext context)
        {
            this.context = context;
        }

        /// <inheritdoc/>
        /// <exception cref="DbUpdateException">
        /// L'enregistrement a échoué, notamment si le nom d'utilisateur est déjà pris.
        /// </exception>
        public async Task Register(User user)
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();   
        }

        /// <inheritdoc/>
        public Task<bool> UserExistsByUsername(string username)
        {
            return Task.FromResult(context.Users.Any(u => u.Username == username));
        }
    }
}
