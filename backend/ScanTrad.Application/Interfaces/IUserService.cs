using ScanTrad.Application.Dtos;

namespace ScanTrad.Application.Interfaces
{
    /// <summary>
    /// Interface pour le service utilisateur.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Enregistre un nouvel utilisateur.
        /// </summary>
        /// <param name="request">Les informations de l'utilisateur à enregistrer.</param>
        /// <returns>Un objet contenant les résultats de l'enregistrement.</returns>
        Task<RegisterResultDto> RegisterUserAsync(RegisterRequestDto request);
    }
}
