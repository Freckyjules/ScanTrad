using ScanTrad.Application.Interfaces;

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
            // Vide pour l'instant : accueillera le compte administrateur (US-3.5, #27)
            // et un compte de test pour la connexion (US-3.2, #24).
            await context.SaveChangesAsync();
        }
    }
}
