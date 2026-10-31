using System.Linq.Expressions;
using OpsPilot.Domain.Entities;
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
            null,                      // AssigneeName: diisi oleh service
            i.CreatedAtUtc,
            i.UpdatedAtUtc,
            i.ResponseDueAtUtc,
            i.ResolutionDueAtUtc);

    public static IncidentDetailDto ToDetailDto(this Incident i, string? creatorName, string? assigneeName) =>
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
            IncidentStatusRules.GetNextStatuses(i.Status));
}