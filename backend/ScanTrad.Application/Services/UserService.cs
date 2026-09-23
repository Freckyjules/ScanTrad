using ScanTrad.Application.Dtos;
using ScanTrad.Application.Interfaces;
using ScanTrad.Domain.Entities;
using ScanTrad.Domain.Repositories;

namespace ScanTrad.Application.Services
{
    /// <summary>
    /// Implémentation du service utilisateur.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IPasswordHasher passwordHasher;
        private readonly IUserRepository userRepository;

        /// <summary>
        /// Initialise une nouvelle instance de la classe <see cref="UserService"/>.
        /// </summary>
        /// <param name="passwordHasher">Le service de hachage des mots de passe.</param>
        /// <param name="userRepository">Le dépôt des utilisateurs.</param>
        public UserService(IPasswordHasher passwordHasher, IUserRepository userRepository)
        {
            this.passwordHasher = passwordHasher;
            this.userRepository = userRepository;
        }

        /// <inheritdoc/>
        public async Task<RegisterResultDto> RegisterUserAsync(RegisterRequestDto request)
        {
            if (request.Password.Length < 8)
            {
                return new RegisterResultDto
                {
                    Success = false,
                    ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères."
                };
            }

            if (await userRepository.UserExistsByUsername(request.Username))
            {
                return new RegisterResultDto
                {
                    Success = false,
                    ErrorMessage = "Le nom d'utilisateur est déjà pris."
                };
            }

            User user = new User
            {
                Username = request.Username,
                PasswordHash = passwordHasher.HashPassword(request.Password)
            };

            await userRepository.Register(user);

            return new RegisterResultDto
            {
                Success = true,
            };
        }
    }
}
