namespace ScanTrad.Domain.Entities
{
    /// <summary>
    /// Représente un utilisateur dans le système.
    /// </summary>
    /// <remarks>
    /// Un utilisateur a toujours un nom et un mot de passe haché : le seul constructeur
    /// public les exige, et aucune propriété n'est modifiable de l'extérieur.
    /// </remarks>
    public class User
    {
        #region Propriétés

        /// <summary>
        /// Identifiant unique de l'utilisateur.
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// Nom d'utilisateur, tel qu'il a été saisi à l'inscription. C'est lui qu'on affiche.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Nom d'utilisateur normalisé par <see cref="NormalizeUsername"/>. C'est sur lui
        /// qu'on compare et qu'on garantit l'unicité : « jules » et « Jules » désignent le
        /// même compte.
        /// </summary>
        public string NormalizedUsername { get; private set; }

        /// <summary>
        /// Mot de passe haché de l'utilisateur.
        /// </summary>
        public string PasswordHash { get; private set; }

        #endregion

        #region Constructeurs

        /// <summary>
        /// Réservé à EF Core, qui en a besoin pour relire la base.
        /// </summary>
        private User()
        {
            Username = string.Empty;
            NormalizedUsername = string.Empty;
            PasswordHash = string.Empty;
        }

        /// <summary>
        /// Crée un utilisateur. Le nom et le hash sont obligatoires ; le nom normalisé
        /// est calculé ici, pour ne jamais se désynchroniser du nom.
        /// </summary>
        /// <param name="username">Le nom d'utilisateur.</param>
        /// <param name="passwordHash">Le mot de passe, déjà haché.</param>
        /// <exception cref="ArgumentException">Le nom ou le hash est vide.</exception>
        public User(string username, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Le nom d'utilisateur est obligatoire.", nameof(username));
            }

            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("Le hash du mot de passe est obligatoire.", nameof(passwordHash));
            }

            Username = username;
            NormalizedUsername = NormalizeUsername(username);
            PasswordHash = passwordHash;
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Rend la forme d'un nom d'utilisateur qui sert à le comparer : tout en
        /// majuscules. C'est le seul endroit où cette règle est écrite ; l'inscription
        /// et la recherche d'un compte passent toutes deux par ici.
        /// </summary>
        /// <remarks>
        /// <see cref="string.ToUpperInvariant"/> plutôt que <see cref="string.ToUpper()"/> :
        /// le résultat ne dépend pas de la langue de la machine (en turc, « i » ne
        /// deviendrait pas « I »).
        /// </remarks>
        /// <param name="username">Le nom tel que saisi.</param>
        /// <returns>Le nom normalisé.</returns>
        public static string NormalizeUsername(string username)
        {
            return username.ToUpperInvariant();
        }

        #endregion
    }
}
