using System.ComponentModel.DataAnnotations;

namespace ScanTrad.Application.Dtos
{
    /// <summary>
    /// Dto pour la requête d'enregistrement d'un nouvel utilisateur.
    /// </summary>
    public class RegisterRequestDto
    {
        #region Propriétés

        /// <summary>
        /// Nom d'utilisateur.
        /// </summary>
        [Required]
        public required string Username { get; set; }

        /// <summary>
        /// Mot de passe.
        /// </summary>
        [Required]
        public required string Password { get; set; }

        #endregion
    }
}
