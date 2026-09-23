using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using ScanTrad.Application.Dtos;
using ScanTrad.Application.Interfaces;

namespace ScanTrad.Api.Controllers
{
    /// <summary>Point d'entrée d'authentification.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class UserController : ControllerBase
    {
        private readonly IUserService userService;

        /// <summary>Injecte le service utilisateur.</summary>
        /// <param name="userService">Le service portant les cas d'usage utilisateur.</param>
        public UserController(IUserService userService)
        {
            this.userService = userService;
        }

        /// <summary>
        /// Enregistre un nouvel utilisateur.
        /// </summary>
        /// <param name="request"> Le nom d'utilisateur et le mot de passe. </param>
        /// <returns></returns>
        [HttpPost("register")]
        [EndpointSummary("Enregistre un nouvel utilisateur.")]
        public async Task<IActionResult> Register(RegisterRequestDto request)
        {
            RegisterResultDto result = await userService.RegisterUserAsync(request);

            if (!result.Success)
            {
                return Unauthorized(result.ErrorMessage);
            }

            return Ok();
        }

    }
}
