using ScanTrad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScanTrad.Domain.Repositories
{
    public interface IUserRepository
    {
        /// <summary>
        /// Enregistre un nouvel utilisateur dans la base de données.
        /// </summary>
        /// <param name="user"> L'utilisateur à enregistrer. </param>
        /// <returns> Une tâche représentant l'opération asynchrone. </returns>
        Task Register(User user);

        /// <summary>
        /// Vérifie si un utilisateur existe déjà dans la base de données en fonction de son nom d'utilisateur.
        /// </summary>
        /// <param name="username"> Le nom d'utilisateur à vérifier. </param>
        /// <returns> true si l'utilisateur existe, false sinon. </returns>
        Task<bool> UserExistsByUsername(string username);
    }
}
