using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Incidents;

public sealed record IncidentSummaryDto(
    Guid Id,
    string Title,
    IncidentSeverity Severity,
    IncidentStatus Status,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? FirstRespondedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset ResponseDueAtUtc,
    DateTimeOffset ResolutionDueAtUtc,
    SlaState ResponseSla,        
    SlaState ResolutionSla);     

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
    SlaState ResponseSla,        
    SlaState ResolutionSla,      
    IReadOnlyList<IncidentStatus> AllowedNextStatuses);