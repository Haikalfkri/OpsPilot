using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Users;

namespace OpsPilot.Api.Controllers;

public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    // Khusus Admin. Daftar user di tim yang sama (berhalaman)
    [Authorize(Policy = Policies.AdminOnly)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserDto>>> List(
        [FromQuery] ListUsersQuery query,
        CancellationToken cancellationToken)
    {
        var users = await userService.ListAsync(query, cancellationToken);
        return Ok(users);
    }
}