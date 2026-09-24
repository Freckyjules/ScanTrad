using ScanTrad.Application.Common;
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
        /// <returns>
        /// Une réussite, ou un échec portant l'une des erreurs de
        /// <see cref="Users.UserErrors"/> : mot de passe trop court, nom
        /// d'utilisateur invalide ou déjà pris.
        /// </returns>
        Task<Result> RegisterUserAsync(RegisterRequestDto request);
    }
}
