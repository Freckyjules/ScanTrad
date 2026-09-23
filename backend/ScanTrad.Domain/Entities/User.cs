using System;
using System.Collections.Generic;
using System.Text;

namespace ScanTrad.Domain.Entities
{
    /// <summary>
    /// Représente un utilisateur dans le système.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Identifiant unique de l'utilisateur.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nom d'utilisateur de l'utilisateur.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Mot de passe haché de l'utilisateur.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;
    }
}
