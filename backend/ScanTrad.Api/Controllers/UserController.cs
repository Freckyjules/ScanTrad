using Microsoft.AspNetCore.Mvc;
using ScanTrad.Api.Extensions;
using ScanTrad.Application.Common;
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
        #region Attributs

        private readonly IUserService userService;

        #endregion

        #region Constructeurs

        /// <summary>Injecte le service utilisateur.</summary>
        /// <param name="userService">Le service portant les cas d'usage utilisateur.</param>
        public UserController(IUserService userService)
        {
            this.userService = userService;
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Enregistre un nouvel utilisateur.
        /// </summary>
        /// <param name="request"> Le nom d'utilisateur et le mot de passe. </param>
        /// <returns>
        /// 201 si le compte est créé, 400 si une règle n'est pas respectée,
        /// 409 si le nom d'utilisateur est déjà pris.
        /// </returns>
        [HttpPost("register")]
        [EndpointSummary("Enregistre un nouvel utilisateur.")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register(RegisterRequestDto request)
        {
            Result result = await userService.RegisterUserAsync(request);

            if (result.IsFailure)
            {
                return this.ToProblem(result.Error);
            }

            return StatusCode(StatusCodes.Status201Created);
        }

        #endregion
    }
}
