using ScanTrad.Domain.Entities;

namespace ScanTrad.Domain.Repositories
{
    /// <summary>
    /// Accès en lecture et en écriture aux utilisateurs enregistrés.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Enregistre un nouvel utilisateur dans la base de données.
        /// </summary>
        /// <param name="user"> L'utilisateur à enregistrer. </param>
        /// <returns> Une tâche représentant l'opération asynchrone. </returns>
        Task AddAsync(User user);

        /// <summary>
        /// Vérifie si un utilisateur existe déjà dans la base de données en fonction de son nom d'utilisateur.
        /// La comparaison ignore les majuscules : « Jules » trouve le compte « jules ».
        /// </summary>
        /// <param name="username"> Le nom d'utilisateur à vérifier. </param>
        /// <returns> true si l'utilisateur existe, false sinon. </returns>
        Task<bool> ExistsByUsernameAsync(string username);
    }
}
