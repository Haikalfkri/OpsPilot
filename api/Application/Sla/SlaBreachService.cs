using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;

namespace OpsPilot.Application.Sla;

public sealed record SlaBreachRunResult(int IncidentsChecked, int BreachesRecorded, int NotificationsCreated);

public interface ISlaBreachService
{
    Task<SlaBreachRunResult> DetectAndRecordAsync(CancellationToken ct);
}

public sealed class SlaBreachService(
    IApplicationDbContext db,
    IIdentityService identity,
    TimeProvider clock) : ISlaBreachService
{
    private const int MaxIncidentsPerRun = 500;

    public async Task<SlaBreachRunResult> DetectAndRecordAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var candidates = await FindCandidatesAsync(now, ct);

        if (candidates.Count == 0)
            return new SlaBreachRunResult(0, 0, 0);

        var adminsByTeam = new Dictionary<Guid, IReadOnlyList<Guid>>();

        var breaches = 0;
        var notifications = 0;

        foreach (var incident in candidates)
        {
            var responseState = SlaEvaluator.EvaluateResponse(
                incident.ResponseDueAtUtc, incident.FirstRespondedAtUtc, incident.ResolvedAtUtc, now);

            var resolutionState = SlaEvaluator.EvaluateResolution(
                incident.ResolutionDueAtUtc, incident.ResolvedAtUtc, now);

            if (responseState == SlaState.Breached && !HasRecord(incident, SlaBreachType.Response))
            {
                notifications += await RecordBreachAsync(
                    incident, SlaBreachType.Response, incident.ResponseDueAtUtc, now, adminsByTeam, ct);
                breaches++;
            }

            if (resolutionState == SlaState.Breached && !HasRecord(incident, SlaBreachType.Resolution))
            {
                notifications += await RecordBreachAsync(
                    incident, SlaBreachType.Resolution, incident.ResolutionDueAtUtc, now, adminsByTeam, ct);
                breaches++;
            }
        }

        await db.SaveChangesAsync(ct);

        return new SlaBreachRunResult(candidates.Count, breaches, notifications);
    }

    // ============================== HELPER ==============================

    private async Task<List<Incident>> FindCandidatesAsync(DateTimeOffset now, CancellationToken ct)
    {
        return await db.Incidents
            .AsNoTracking()
            .Include(i => i.SlaBreaches)    // sekalian muat catatan pelanggaran yang sudah ada
            .Where(i => i.Status == IncidentStatus.Open || i.Status == IncidentStatus.Investigating)
            .Where(i =>
                (i.ResponseDueAtUtc < now
                    && !i.SlaBreaches.Any(b => b.BreachType == SlaBreachType.Response)
                    && (i.FirstRespondedAtUtc == null || i.FirstRespondedAtUtc > i.ResponseDueAtUtc))
                ||
                (i.ResolutionDueAtUtc < now
                    && !i.SlaBreaches.Any(b => b.BreachType == SlaBreachType.Resolution)))
            .OrderBy(i => i.ResolutionDueAtUtc)
            .Take(MaxIncidentsPerRun)
            .ToListAsync(ct);
    }

    private static bool HasRecord(Incident incident, SlaBreachType type) =>
        incident.SlaBreaches.Any(b => b.BreachType == type);

    private async Task<int> RecordBreachAsync(
        Incident incident,
        SlaBreachType type,
        DateTimeOffset dueAtUtc,
        DateTimeOffset now,
        Dictionary<Guid, IReadOnlyList<Guid>> adminsByTeam,
        CancellationToken ct)
    {
        var isResponse = type == SlaBreachType.Response;
        var label = isResponse ? "response" : "resolution";

        db.IncidentSlaBreaches.Add(IncidentSlaBreach.Create(incident.Id, type, dueAtUtc, now));

        db.IncidentActivities.Add(IncidentActivity.Create(
            incident.Id,
            actorId: null,
            isResponse ? ActivityType.ResponseSlaBreached : ActivityType.ResolutionSlaBreached,
            $"SLA {label} target breached",
            JsonSerializer.Serialize(new { dueAtUtc }),
            now));

        var recipients = await GetRecipientsAsync(incident, adminsByTeam, ct);

        foreach (var recipientId in recipients)
        {
            db.Notifications.Add(Notification.Create(
                recipientId,
                incident.Id,
                isResponse ? NotificationType.ResponseSlaBreached : NotificationType.ResolutionSlaBreached,
                $"SLA {label} target breached",
                $"\"{incident.Title}\" ({incident.Severity}) missed its {label} target.",
                now));
        }

        return recipients.Count;
    }

    private async Task<IReadOnlyList<Guid>> GetRecipientsAsync(
        Incident incident, Dictionary<Guid, IReadOnlyList<Guid>> adminsByTeam, CancellationToken ct)
    {
        if (incident.AssigneeId is { } assigneeId)
            return new[] { assigneeId };

        if (!adminsByTeam.TryGetValue(incident.TeamId, out var admins))
        {
            admins = await identity.GetActiveAdminIdsAsync(incident.TeamId, ct);
            adminsByTeam[incident.TeamId] = admins;
        }

        return admins;
    }
}