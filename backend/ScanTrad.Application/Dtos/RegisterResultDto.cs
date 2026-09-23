using System;
using System.Collections.Generic;
using System.Text;

namespace ScanTrad.Application.Dtos
{
    /// <summary>
    /// Résultat de l'inscription d'un utilisateur.
    /// </summary>
    public class RegisterResultDto
    {
        /// <summary><c>true</c> si l'utilisateur a été enregistré, <c>false</c> sinon.</summary>
        public bool Success { get; set; }

        /// <summary>Message d'erreur en cas d'échec.</summary>
        public string? ErrorMessage { get; set; }
    }
}
