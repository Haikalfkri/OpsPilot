using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Api.Auth;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Incidents;

namespace OpsPilot.Api.Controllers
{
    public sealed class IncidentsController(IIncidentService incidentService) : ApiControllerBase
    {
        [Authorize(Policy = Policies.AnyTeamMember)]
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<IncidentSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResult<IncidentSummaryDto>>> List(
        [FromQuery] ListIncidentsQuery query,
        CancellationToken cancellationToken)
        {
            var result = await incidentService.ListAsync(query, cancellationToken);
            return Ok(result);
        }

        [Authorize(Policy = Policies.AnyTeamMember)]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IncidentDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var incident = await incidentService.GetByIdAsync(id, cancellationToken);
            return Ok(incident);
        }

        [Authorize(Policy = Policies.CanManageIncidents)]
        [HttpPost]
        [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IncidentDetailDto>> Create(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken)
        {
            var created = await incidentService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [Authorize(Policy = Policies.CanManageIncidents)]
        [HttpPatch("{id:guid}")]
        [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<IncidentDetailDto>> Update(
        Guid id,
        [FromBody] UpdateIncidentRequest request,
        CancellationToken cancellationToken)
        {
            var updated = await incidentService.UpdateAsync(id, request, cancellationToken);
            return Ok(updated);
        }

        [Authorize(Policy = Policies.CanManageIncidents)]
        [HttpPost("{id:guid}/status")]
        [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<IncidentDetailDto>> ChangeStatus(
            Guid id,
            [FromBody] ChangeStatusRequest request,
            CancellationToken cancellationToken)
        {
            var updated = await incidentService.ChangeStatusAsync(id, request, cancellationToken);
            return Ok(updated);
        }

        [Authorize(Policy = Policies.CanManageIncidents)]
        [HttpPut("{id:guid}/assignee")]
        [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<IncidentDetailDto>> Assign(
        Guid id,
        [FromBody] AssignIncidentRequest request,
        CancellationToken cancellationToken)
        {
            var updated = await incidentService.AssignAsync(id, request, cancellationToken);
            return Ok(updated);
        }
    }
}
