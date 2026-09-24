using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScanTrad.Domain.Entities;
using ScanTrad.Infrastructure.Data;

namespace ScanTrad.IntegrationTests.Controllers
{
    /// <summary>
    /// Éprouve <c>POST api/user/register</c> de bout en bout : requête HTTP réelle,
    /// validation, service, repository et base SQLite. Un test par critère de
    /// l'US-3.1 (créer un compte).
    /// </summary>
    /// <remarks>
    /// Tous les tests de la classe partagent une même API et une même base, démarrées
    /// une seule fois. Chaque test invente donc son propre nom d'utilisateur, pour ne
    /// jamais dépendre de ce qu'un autre test a créé.
    /// </remarks>
    public class UserControllerRegisterTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private const string Route = "/api/user/register";
        private const string MotDePasseValide = "motdepasse1";

        private readonly CustomWebApplicationFactory<Program> api;
        private readonly HttpClient client;

        /// <summary>
        /// Initialise les tests avec l'API partagée.
        /// </summary>
        /// <param name="api">L'API démarrée sur sa base en mémoire.</param>
        public UserControllerRegisterTests(CustomWebApplicationFactory<Program> api)
        {
            this.api = api;
            client = api.CreateClient();
        }

        /// <summary>
        /// Des données valides créent le compte : 201, sans corps.
        /// </summary>
        [Fact]
        public async Task Register_DonneesValides_Renvoie201()
        {
            HttpResponseMessage reponse = await Inscrire(NomInedit(), MotDePasseValide);

            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        }

        /// <summary>
        /// Le compte créé est bien en base, et son mot de passe n'y est jamais en clair.
        /// </summary>
        [Fact]
        public async Task Register_DonneesValides_EnregistreLeCompteAvecUnMotDePasseHache()
        {
            string nom = NomInedit();

            await Inscrire(nom, MotDePasseValide);

            using IServiceScope scope = api.Services.CreateScope();
            ScanTradDbContext contexte = scope.ServiceProvider.GetRequiredService<ScanTradDbContext>();
            User enregistre = await contexte.Users.SingleAsync(
                u => u.Username == nom, TestContext.Current.CancellationToken);

            Assert.NotEqual(MotDePasseValide, enregistre.PasswordHash);
            Assert.DoesNotContain(MotDePasseValide, enregistre.PasswordHash);
        }

        /// <summary>
        /// Un nom déjà pris est refusé : 409, avec le code <c>User.UsernameTaken</c>.
        /// </summary>
        [Fact]
        public async Task Register_NomDejaPris_Renvoie409()
        {
            string nom = NomInedit();
            await Inscrire(nom, MotDePasseValide);

            HttpResponseMessage reponse = await Inscrire(nom, MotDePasseValide);

            Assert.Equal(HttpStatusCode.Conflict, reponse.StatusCode);
            Assert.Equal("User.UsernameTaken", await CodeDErreur(reponse));
        }

        /// <summary>
        /// Le nom ne tient pas compte des majuscules : « jules » puis « JULES » est
        /// refusé comme un nom déjà pris.
        /// </summary>
        [Fact]
        public async Task Register_MemeNomAvecDAutresMajuscules_Renvoie409()
        {
            string nom = NomInedit();
            await Inscrire(nom, MotDePasseValide);

            HttpResponseMessage reponse = await Inscrire(nom.ToUpperInvariant(), MotDePasseValide);

            Assert.Equal(HttpStatusCode.Conflict, reponse.StatusCode);
            Assert.Equal("User.UsernameTaken", await CodeDErreur(reponse));
        }

        /// <summary>
        /// Le nom est conservé tel qu'il a été saisi, majuscules comprises, et sa forme
        /// normalisée (tout en majuscules) est enregistrée à côté.
        /// </summary>
        [Fact]
        public async Task Register_NomAvecDesMajuscules_ConserveLeNomSaisiEtEnregistreSaFormeNormalisee()
        {
            string nom = "Ab" + Guid.NewGuid().ToString("N")[..10];

            await Inscrire(nom, MotDePasseValide);

            using IServiceScope scope = api.Services.CreateScope();
            ScanTradDbContext contexte = scope.ServiceProvider.GetRequiredService<ScanTradDbContext>();
            User enregistre = await contexte.Users.SingleAsync(
                u => u.Username == nom, TestContext.Current.CancellationToken);

            Assert.Equal(nom, enregistre.Username);
            Assert.Equal(nom.ToUpperInvariant(), enregistre.NormalizedUsername);
        }

        /// <summary>
        /// Un mot de passe de moins de 8 caractères est refusé : 400,
        /// <c>User.PasswordTooShort</c>.
        /// </summary>
        [Fact]
        public async Task Register_MotDePasseTropCourt_Renvoie400()
        {
            HttpResponseMessage reponse = await Inscrire(NomInedit(), "1234567");

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
            Assert.Equal("User.PasswordTooShort", await CodeDErreur(reponse));
        }

        /// <summary>
        /// Un mot de passe de plus de 128 caractères est refusé : 400,
        /// <c>User.PasswordTooLong</c>.
        /// </summary>
        [Fact]
        public async Task Register_MotDePasseTropLong_Renvoie400()
        {
            HttpResponseMessage reponse = await Inscrire(NomInedit(), new string('a', 129));

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
            Assert.Equal("User.PasswordTooLong", await CodeDErreur(reponse));
        }

        /// <summary>
        /// Les bornes du mot de passe sont incluses : 8 et 128 caractères passent.
        /// </summary>
        /// <param name="longueur">La longueur du mot de passe, exactement sur une borne.</param>
        [Theory]
        [InlineData(8)]
        [InlineData(128)]
        public async Task Register_MotDePasseSurUneBorne_Renvoie201(int longueur)
        {
            HttpResponseMessage reponse = await Inscrire(NomInedit(), new string('a', longueur));

            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        }

        /// <summary>
        /// Un nom trop court, trop long, ou avec un caractère interdit est refusé :
        /// 400, <c>User.InvalidUsername</c>.
        /// </summary>
        /// <param name="nom">Le nom invalide.</param>
        [Theory]
        [InlineData("jo")]
        [InlineData("abcdefghijklmnopqrstu")]
        [InlineData("jules!")]
        [InlineData("jules dupont")]
        [InlineData("jérôme")]
        public async Task Register_NomInvalide_Renvoie400(string nom)
        {
            HttpResponseMessage reponse = await Inscrire(nom, MotDePasseValide);

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
            Assert.Equal("User.InvalidUsername", await CodeDErreur(reponse));
        }

        /// <summary>
        /// Les bornes du nom sont incluses : 3 et 20 caractères passent, tout comme
        /// « _ » et « - ».
        /// </summary>
        /// <param name="prefixe">Le début du nom, complété pour le rendre unique.</param>
        /// <param name="longueur">La longueur totale du nom.</param>
        [Theory]
        [InlineData("a", 3)]
        [InlineData("a", 20)]
        [InlineData("a_b-", 12)]
        public async Task Register_NomSurUneBorne_Renvoie201(string prefixe, int longueur)
        {
            string nom = (prefixe + Guid.NewGuid().ToString("N"))[..longueur];

            HttpResponseMessage reponse = await Inscrire(nom, MotDePasseValide);

            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        }

        /// <summary>
        /// Un champ absent du JSON est refusé avant même d'atteindre le contrôleur : 400.
        /// </summary>
        [Fact]
        public async Task Register_MotDePasseAbsent_Renvoie400()
        {
            HttpResponseMessage reponse = await client.PostAsJsonAsync(
                Route, new { username = NomInedit() }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        /// <summary>
        /// Un champ vide est refusé avant même d'atteindre le contrôleur : 400.
        /// </summary>
        [Fact]
        public async Task Register_NomVide_Renvoie400()
        {
            HttpResponseMessage reponse = await Inscrire("", MotDePasseValide);

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        /// <summary>
        /// Envoie une demande d'inscription.
        /// </summary>
        /// <param name="nom">Le nom d'utilisateur.</param>
        /// <param name="motDePasse">Le mot de passe.</param>
        /// <returns>La réponse de l'API.</returns>
        private Task<HttpResponseMessage> Inscrire(string nom, string motDePasse)
        {
            return client.PostAsJsonAsync(
                Route, new { username = nom, password = motDePasse }, TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// Invente un nom d'utilisateur valide qu'aucun autre test n'utilise.
        /// </summary>
        /// <returns>Un nom de 13 caractères.</returns>
        private static string NomInedit()
        {
            return "u" + Guid.NewGuid().ToString("N")[..12];
        }

        /// <summary>
        /// Lit l'identifiant stable de l'erreur dans le corps <c>ProblemDetails</c>.
        /// </summary>
        /// <param name="reponse">La réponse en erreur.</param>
        /// <returns>La valeur du champ <c>code</c>.</returns>
        private static async Task<string?> CodeDErreur(HttpResponseMessage reponse)
        {
            using JsonDocument corps = JsonDocument.Parse(
                await reponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            return corps.RootElement.GetProperty("code").GetString();
        }
    }
}
