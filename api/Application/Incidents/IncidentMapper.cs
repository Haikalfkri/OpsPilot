using System.Linq.Expressions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Rules;

namespace OpsPilot.Application.Incidents;

public static class IncidentMapper
{
    public static readonly Expression<Func<Incident, IncidentSummaryDto>> SummaryProjection =
        i => new IncidentSummaryDto(
            i.Id,
            i.Title,
            i.Severity,
            i.Status,
            i.AssigneeId,
            null,                      
            i.CreatedAtUtc,
            i.UpdatedAtUtc,
            i.FirstRespondedAtUtc,
            i.ResolvedAtUtc,
            i.ResponseDueAtUtc,
            i.ResolutionDueAtUtc,
            SlaState.Pending,         
            SlaState.Pending);         

    public static IncidentDetailDto ToDetailDto(
        this Incident i, string? creatorName, string? assigneeName, DateTimeOffset now) =>
        new(
            i.Id,
            i.TeamId,
            i.Title,
            i.Description,
            i.Severity,
            i.Status,
            i.CreatedById,
            creatorName,
            i.AssigneeId,
            assigneeName,
            i.CreatedAtUtc,
            i.UpdatedAtUtc,
            i.FirstRespondedAtUtc,
            i.ResolvedAtUtc,
            i.ClosedAtUtc,
            i.ResponseDueAtUtc,
            i.ResolutionDueAtUtc,
            i.GetResponseSla(now),
            i.GetResolutionSla(now),
            IncidentStatusRules.GetNextStatuses(i.Status));
}