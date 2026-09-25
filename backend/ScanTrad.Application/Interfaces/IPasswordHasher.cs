namespace ScanTrad.Application.Interfaces
{
    /// <summary>
    /// Interface pour le service de hachage des mots de passe.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Hache le mot de passe
        /// </summary>
        /// <param name="password">Le mot de passe à hacher</param>
        /// <returns>Le mot de passe haché</returns>
        string HashPassword(string password);
    }
}
