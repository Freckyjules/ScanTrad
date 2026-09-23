using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ScanTrad.Application.Dtos
{
    /// <summary>
    /// Dto pour la requête d'enregistrement d'un nouvel utilisateur.
    /// </summary>
    public class RegisterRequestDto
    {
        /// <summary>
        /// Nom d'utilisateur.
        /// </summary>
        [Required]
        public string Username { get; set; }
        /// <summary>
        /// Mot de passe.
        /// </summary>
        [Required]
        public string Password { get; set; }
    }
}
