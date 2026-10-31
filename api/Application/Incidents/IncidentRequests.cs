using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Incidents
{
    public sealed record CreateIncidentRequest(
        string Title, 
        string Description, 
        IncidentSeverity? 
        Severity, Guid? AssigneeId = null
    );

    public sealed record UpdateIncidentRequest(
        string? Title = null,
        string? Description = null,
        IncidentSeverity? Severity = null
    );

    public sealed record ChangeStatusRequest(IncidentStatus? Status);

    public sealed record AssignIncidentRequest(Guid? AssigneeId);
}
