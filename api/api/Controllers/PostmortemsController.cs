using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Postmortems;

namespace OpsPilot.Api.Controllers
{
    public sealed class PostmortemsController(IPostmortemService postmortemService) : ApiControllerBase
    {
        private const string PostmortemRoute = "~/api/v1/incidents/{id:guid}/postmortem";

        [Authorize(Policy = Policies.AnyTeamMember)]
        [HttpGet(PostmortemRoute)]
        [ProducesResponseType(typeof(PostmortemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PostmortemDto>> Get(Guid id, CancellationToken cancellationToken)
        {
            var postmortem = await postmortemService.GetAsync(id, cancellationToken);
            return Ok(postmortem);
        }

        [Authorize(Policy = Policies.CanManageIncidents)]
        [HttpPut(PostmortemRoute)]
        [ProducesResponseType(typeof(PostmortemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(PostmortemDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PostmortemDto>> Save(
            Guid id,
            [FromBody] SavePostmortemRequest request,
            CancellationToken cancellationToken)
        {
            var result = await postmortemService.SaveAsync(id, request, cancellationToken);

            if (result.Created)
                return Created($"/api/v1/incidents/{id}/postmortem", result.Postmortem);

            return Ok(result.Postmortem);
        }
    }
}
