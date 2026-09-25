using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScanTrad.Application.Interfaces;
using ScanTrad.Infrastructure.Data;

namespace ScanTrad.IntegrationTests
{
    /// <summary>
    /// Démarre l'API en mémoire en remplaçant la base SQLite sur fichier par une base
    /// SQLite « :memory: », créée par les migrations et alimentée une fois avant le
    /// premier test. La base de développement n'est jamais touchée.
    /// </summary>
    /// <typeparam name="TProgram">Le point d'entrée de l'API à démarrer.</typeparam>
    public class CustomWebApplicationFactory<TProgram>
        : WebApplicationFactory<TProgram> where TProgram : class
    {
        #region Méthodes

        /// <summary>
        /// Remplace l'enregistrement du <see cref="ScanTradDbContext"/> par une base en
        /// mémoire, et démarre l'API dans l'environnement « Test ».
        /// </summary>
        /// <param name="builder">L'hôte web en cours de construction.</param>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                RemoveService(services, typeof(IDbContextOptionsConfiguration<ScanTradDbContext>));
                RemoveService(services, typeof(DbConnection));

                // Connexion ouverte et gardée en singleton : SQLite détruit une base
                // « :memory: » dès que sa dernière connexion se ferme.
                services.AddSingleton<DbConnection>(_ =>
                {
                    SqliteConnection connection = new SqliteConnection("DataSource=:memory:");
                    connection.Open();
                    return connection;
                });

                services.AddDbContext<ScanTradDbContext>((container, options) =>
                    options.UseSqlite(container.GetRequiredService<DbConnection>()));
            });

            builder.UseEnvironment("Test");
        }

        /// <summary>
        /// Démarre l'API, puis insère les données de test avant l'exécution des tests.
        /// </summary>
        /// <remarks>
        /// Le schéma n'est pas créé ici : <c>Program.cs</c> applique déjà les migrations
        /// au démarrage, sur la base en mémoire. Les tests éprouvent ainsi les vraies
        /// migrations. Un <c>EnsureCreated()</c> serait inutile une fois les tables
        /// présentes, et ferait échouer les migrations s'il passait avant elles.
        /// </remarks>
        /// <param name="builder">L'hôte en cours de construction.</param>
        /// <returns>L'hôte démarré, base alimentée.</returns>
        protected override IHost CreateHost(IHostBuilder builder)
        {
            IHost host = base.CreateHost(builder);

            using IServiceScope scope = host.Services.CreateScope();
            ScanTradDbContext context = scope.ServiceProvider.GetRequiredService<ScanTradDbContext>();
            IPasswordHasher hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            DbInitializer.SeedAsync(context, hasher).GetAwaiter().GetResult();

            return host;
        }

        #endregion

        #region Méthodes privées

        /// <summary>
        /// Retire un service enregistré du conteneur, s'il existe.
        /// </summary>
        /// <param name="services">Le conteneur de services.</param>
        /// <param name="serviceType">Le type de service à retirer.</param>
        private static void RemoveService(IServiceCollection services, Type serviceType)
        {
            ServiceDescriptor? descriptor = services.SingleOrDefault(d => d.ServiceType == serviceType);
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }
        }

        #endregion
    }
}
