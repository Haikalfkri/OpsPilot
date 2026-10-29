using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Teams;

namespace OpsPilot.Api.Controllers
{
    public sealed class TeamsController(ITeamsService teamsService) : ApiControllerBase
    {
        // tim milik user yang sedang login
        [Authorize(Policy = Policies.AnyTeamMember)]
        [HttpGet("current")]
        [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<TeamDto>> GetCurrent(CancellationToken cancellationToken)
        {
            var team = await teamsService.GetCurrentAsync(cancellationToken);
            return Ok(team);
        }
    }
}
