using Microsoft.EntityFrameworkCore;
using ScanTrad.Domain.Entities;

namespace ScanTrad.Infrastructure.Data
{
    /// <summary>
    /// Contexte EF Core de ScanTrad : les tables de la base et leur configuration.
    /// </summary>
    public class ScanTradDbContext : DbContext
    {
        #region Propriétés

        /// <summary>Table des utilisateurs.</summary>
        public DbSet<User> Users => Set<User>();

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise le contexte avec ses options (fournisseur et chaîne de connexion),
        /// données par l'injection de dépendances.
        /// </summary>
        /// <param name="options">Les options du contexte.</param>
        public ScanTradDbContext(DbContextOptions<ScanTradDbContext> options)
            : base(options)
        {
        }

        #endregion

        #region Méthodes

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(20);
                entity.Property(u => u.NormalizedUsername).IsRequired().HasMaxLength(20);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);

                // L'unicité porte sur le nom normalisé : « jules » et « Jules » ne
                // peuvent pas coexister, même inscrits au même instant.
                entity.HasIndex(u => u.NormalizedUsername).IsUnique();
            });
        }

        #endregion
    }
}
