using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Auth;

namespace OpsPilot.Api.Controllers
{
    public sealed class AuthController(IAuthService authService) : ApiControllerBase
    {
        // masuk dengan email dan password. mengembalikan token jwt
        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponse>> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken
        )
        {
            var response = await authService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }

        // Data user yang sedang login (profil, tim, role)
        [Authorize(Policy = Policies.AnyTeamMember)]
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
        {
            var user = await authService.GetCurrentUserAsync(cancellationToken);
            return Ok(user);
        }
    }
}
