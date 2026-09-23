using ScanTrad.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScanTrad.Infrastructure.Data
{
    /// <summary>
    /// Permet d'initialiser la base de données avec des données par défaut.
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// Initialise la base de données avec des données par défaut.
        /// </summary>
        /// <param name="context"> Le contexte EF Core cible </param>
        /// <param name="passwordHasher"> Le service de hachage de mot de passe </param>
        public static async Task SeedAsync(ScanTradDbContext context, IPasswordHasher passwordHasher)
        {
            if (context.Users.Any())
            {
                return;
            }
            // Permet ici de mettre en base par défaut
            await context.SaveChangesAsync();
        }
    }
}
