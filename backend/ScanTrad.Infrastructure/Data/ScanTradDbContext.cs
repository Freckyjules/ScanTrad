using Microsoft.EntityFrameworkCore;
using ScanTrad.Domain.Entities;

namespace ScanTrad.Infrastructure.Data
{
    /// <summary>
    /// DbContext pour l'application Network Operations Center.
    /// </summary>
    public class ScanTradDbContext : DbContext
    {
        public ScanTradDbContext(DbContextOptions<ScanTradDbContext> options)
        : base(options)
        {
        }

        /// <summary>Table des utilisateurs.</summary>
        public DbSet<User> Users => Set<User>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
                entity.HasIndex(u => u.Username).IsUnique();
            });
        }
    }
}
