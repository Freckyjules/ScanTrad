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
        /// Nom d'utilisateur de l'utilisateur.
        /// </summary>
        public string Username { get; private set; }

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
            PasswordHash = string.Empty;
        }

        /// <summary>
        /// Crée un utilisateur. Le nom et le hash sont obligatoires.
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
            PasswordHash = passwordHash;
        }

        #endregion
    }
}
