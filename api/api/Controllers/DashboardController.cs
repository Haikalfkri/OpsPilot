using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Dashboard;

namespace OpsPilot.Api.Controllers
{
    [Authorize(Policy = Policies.AnyTeamMember)]
    public sealed class DashboardController(IDashboardService dashboardService) : ApiControllerBase
    {
        [HttpGet("summary")]
        [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DashboardSummaryDto>> GetSummary(
            [FromQuery] DashboardQuery query,
            CancellationToken cancellationToken)
        {
            var result = await dashboardService.GetSummaryAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("incident-breakdown")]
        [ProducesResponseType(typeof(DashboardBreakdownDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DashboardBreakdownDto>> GetBreakdown(
            [FromQuery] DashboardQuery query,
            CancellationToken cancellationToken)
        {
            var result = await dashboardService.GetBreakdownAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("trends")]
        [ProducesResponseType(typeof(DashboardTrendsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DashboardTrendsDto>> GetTrends(
            [FromQuery] DashboardQuery query,
            CancellationToken cancellationToken)
        {
            var result = await dashboardService.GetTrendsAsync(query, cancellationToken);
            return Ok(result);
        }
    }
}
