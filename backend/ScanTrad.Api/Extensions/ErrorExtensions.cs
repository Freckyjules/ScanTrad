using Microsoft.AspNetCore.Mvc;
using ScanTrad.Application.Common;

namespace ScanTrad.Api.Extensions
{
    /// <summary>
    /// Traduit les erreurs métier en réponses HTTP. C'est le seul endroit de l'API
    /// où une famille d'erreur devient un code HTTP.
    /// </summary>
    public static class ErrorExtensions
    {
        #region Méthodes

        /// <summary>
        /// Construit la réponse HTTP d'une erreur métier, au format standard
        /// <see cref="ProblemDetails"/> : 400 pour une validation, 409 pour un conflit,
        /// 404 pour une ressource introuvable.
        /// </summary>
        /// <param name="controller">Le contrôleur qui répond.</param>
        /// <param name="error">L'erreur rendue par le cas d'usage.</param>
        /// <returns>
        /// La réponse HTTP, dont le corps porte le message (<c>title</c>) et
        /// l'identifiant stable de l'erreur (<c>code</c>).
        /// </returns>
        public static IActionResult ToProblem(this ControllerBase controller, Error error)
        {
            int status = error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };

            ProblemDetails problem = new ProblemDetails
            {
                Status = status,
                Title = error.Message
            };
            problem.Extensions["code"] = error.Code;

            return controller.StatusCode(status, problem);
        }

        #endregion
    }
}
