using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DashboardQuery query, CancellationToken ct);
    Task<DashboardBreakdownDto> GetBreakdownAsync(DashboardQuery query, CancellationToken ct);
    Task<DashboardTrendsDto> GetTrendsAsync(DashboardQuery query, CancellationToken ct);
}

public sealed class DashboardService(
    IValidator<DashboardQuery> validator,
    IApplicationDbContext db,
    IDashboardCache cache,
    ICurrentUser currentUser,
    TimeProvider clock) : IDashboardService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<DashboardSummaryDto> GetSummaryAsync(DashboardQuery query, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(query, ct);

        var teamId = currentUser.TeamId;
        var days = query.Days;

        return await cache.GetOrCreateAsync(
            DashboardCacheKeys.Summary(teamId, days),
            CacheTtl,
            token => BuildSummaryAsync(teamId, days, token),
            ct);
    }

    private async Task<DashboardSummaryDto> BuildSummaryAsync(Guid teamId, int days, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var period = DashboardPeriod.Create(days, now);

        var incidents = db.Incidents.AsNoTracking().Where(i => i.TeamId == teamId);

        var active = incidents.Where(i => i.Status == IncidentStatus.Open || i.Status == IncidentStatus.Investigating);

        var openCount = await active.CountAsync(ct);

        var criticalHighCount = await active.CountAsync(
            i => i.Severity == IncidentSeverity.Critical || i.Severity == IncidentSeverity.High, ct);

        var breachedCount = await active.CountAsync(i =>
            (i.ResponseDueAtUtc < now
                && (i.FirstRespondedAtUtc == null || i.FirstRespondedAtUtc > i.ResponseDueAtUtc))
            || i.ResolutionDueAtUtc < now, ct);

        var resolved = await incidents
            .Where(i => i.ResolvedAtUtc != null && i.ResolvedAtUtc >= period.FromUtc)
            .Select(i => new { i.CreatedAtUtc, i.ResolvedAtUtc })
            .ToListAsync(ct);

        double? averageMinutes = null;
        if (resolved.Count > 0)
        {
            averageMinutes = Math.Round(
                resolved.Average(x => (x.ResolvedAtUtc!.Value - x.CreatedAtUtc).TotalMinutes), 1);
        }

        return new DashboardSummaryDto(
            period.ToDto(),
            openCount,
            criticalHighCount,
            breachedCount,
            resolved.Count,
            averageMinutes,
            now);
    }

    public async Task<DashboardBreakdownDto> GetBreakdownAsync(DashboardQuery query, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(query, ct);

        var teamId = currentUser.TeamId;
        var days = query.Days;

        return await cache.GetOrCreateAsync(
            DashboardCacheKeys.Breakdown(teamId, days),
            CacheTtl,
            token => BuildBreakdownAsync(teamId, days, token),
            ct);
    }

    private async Task<DashboardBreakdownDto> BuildBreakdownAsync(Guid teamId, int days, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var period = DashboardPeriod.Create(days, now);

        var inPeriod = db.Incidents
            .AsNoTracking()
            .Where(i => i.TeamId == teamId && i.CreatedAtUtc >= period.FromUtc);

        var severityRows = await inPeriod
            .GroupBy(i => i.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var statusRows = await inPeriod
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new DashboardBreakdownDto(
            period.ToDto(),
            DashboardMapper.ToSeverityCounts(severityRows.ToDictionary(r => r.Severity, r => r.Count)),
            DashboardMapper.ToStatusCounts(statusRows.ToDictionary(r => r.Status, r => r.Count)),
            now);
    }

    public async Task<DashboardTrendsDto> GetTrendsAsync(DashboardQuery query, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(query, ct);

        var teamId = currentUser.TeamId;
        var days = query.Days;

        return await cache.GetOrCreateAsync(
            DashboardCacheKeys.Trends(teamId, days),
            CacheTtl,
            token => BuildTrendsAsync(teamId, days, token),
            ct);
    }

    private async Task<DashboardTrendsDto> BuildTrendsAsync(Guid teamId, int days, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var period = DashboardPeriod.Create(days, now);

        var createdTimes = await db.Incidents
            .AsNoTracking()
            .Where(i => i.TeamId == teamId && i.CreatedAtUtc >= period.FromUtc)
            .Select(i => i.CreatedAtUtc)
            .ToListAsync(ct);

        var resolvedTimes = await db.Incidents
            .AsNoTracking()
            .Where(i => i.TeamId == teamId && i.ResolvedAtUtc != null && i.ResolvedAtUtc >= period.FromUtc)
            .Select(i => i.ResolvedAtUtc)
            .ToListAsync(ct);

        var points = DashboardMapper.ToTrendPoints(
            period,
            createdTimes,
            resolvedTimes.Where(t => t.HasValue).Select(t => t!.Value));

        return new DashboardTrendsDto(period.ToDto(), points, now);
    }
}