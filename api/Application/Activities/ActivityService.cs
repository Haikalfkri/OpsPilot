using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Extensions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;

namespace OpsPilot.Application.Activities;

public interface IActivityService
{
    Task<PagedResult<ActivityDto>> ListAsync(Guid incidentId, PageQuery query, CancellationToken ct);
}

public sealed class ActivityService(
    IValidator<PageQuery> validator,
    IApplicationDbContext db,
    IIdentityService identity,
    ICurrentUser currentUser) : IActivityService
{
    public async Task<PagedResult<ActivityDto>> ListAsync(Guid incidentId, PageQuery query, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(query, ct);
        await db.EnsureIncidentInTeamAsync(incidentId, currentUser.TeamId, ct);

        var activities = db.IncidentActivities
            .AsNoTracking()
            .Where(a => a.IncidentId == incidentId);

        var totalCount = await activities.CountAsync(ct);

        var items = await activities
            .OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ActivityMapper.Projection)
            .ToListAsync(ct);

        var actorIds = items
            .Where(i => i.ActorId.HasValue)
            .Select(i => i.ActorId!.Value)
            .Distinct()
            .ToList();

        var names = await identity.GetDisplayNamesAsync(actorIds, ct);

        var withNames = items
            .Select(i => i with
            {
                ActorName = i.ActorId is { } actorId ? names.GetValueOrDefault(actorId) : "System"
            })
            .ToList();

        return new PagedResult<ActivityDto>(withNames, query.Page, query.PageSize, totalCount);
    }
}