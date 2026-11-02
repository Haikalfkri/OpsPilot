using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Sla;

namespace OpsPilot.Api.Controllers
{
    public sealed class SlaController(ISlaService slaService) : ApiControllerBase
    {
        [Authorize(Policy = Policies.AnyTeamMember)]
        [HttpGet("policy")]
        [ProducesResponseType(typeof(SlaPolicyDto), StatusCodes.Status200OK)]
        public ActionResult<SlaPolicyDto> GetPolicy()
        {
            return Ok(slaService.GetPolicy());
        }
    }
}
