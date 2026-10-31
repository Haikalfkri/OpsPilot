using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Incidents
{
    public sealed record IncidentSummaryDto(
        Guid Id,
        string Title,
        IncidentSeverity Severity,
        IncidentStatus Status,
        Guid? AssigneeId,
        string? AssigneeName,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DateTimeOffset ResponseDueAtUtc,
        DateTimeOffset ResolutionDueAtUtc);

    public sealed record IncidentDetailDto(
    Guid Id,
    Guid TeamId,
    string Title,
    string Description,
    IncidentSeverity Severity,
    IncidentStatus Status,
    Guid CreatedById,
    string? CreatedByName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? FirstRespondedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    DateTimeOffset ResponseDueAtUtc,
    DateTimeOffset ResolutionDueAtUtc,

    IReadOnlyList<IncidentStatus> AllowedNextStatuses);
}
