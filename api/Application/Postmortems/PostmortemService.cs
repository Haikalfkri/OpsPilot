using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Extensions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Postmortems;

public interface IPostmortemService
{
    Task<PostmortemDto> GetAsync(Guid incidentId, CancellationToken ct);
    Task<SavePostmortemResult> SaveAsync(Guid incidentId, SavePostmortemRequest request, CancellationToken ct);
}

public sealed class PostmortemService(
    IValidator<SavePostmortemRequest> validator,
    IApplicationDbContext db,
    IIdentityService identity,
    ICurrentUser currentUser,
    TimeProvider clock) : IPostmortemService
{
    public async Task<PostmortemDto> GetAsync(Guid incidentId, CancellationToken ct)
    {
        await db.EnsureIncidentInTeamAsync(incidentId, currentUser.TeamId, ct);

        var postmortem = await db.Postmortems
                             .AsNoTracking()
                             .FirstOrDefaultAsync(p => p.IncidentId == incidentId, ct)
                         ?? throw new NotFoundException("Postmortem not found.");

        return await ToDtoAsync(postmortem, ct);
    }

    public async Task<SavePostmortemResult> SaveAsync(Guid incidentId, SavePostmortemRequest request, CancellationToken ct)
    {
        if (currentUser.Role is not (UserRole.Admin or UserRole.Responder))
            throw new ForbiddenException();

        await validator.ValidateAndThrowAsync(request, ct);

        var teamId = currentUser.TeamId;
        var incident = await db.Incidents
                           .AsNoTracking()
                           .FirstOrDefaultAsync(i => i.Id == incidentId && i.TeamId == teamId, ct)
                       ?? throw new NotFoundException("Incident not found.");

        incident.EnsureCanHavePostmortem();

        var summary = request.Summary.Trim();
        var rootCause = request.RootCause.Trim();
        var impact = request.Impact.Trim();
        var correctiveActions = request.CorrectiveActions.Trim();

        var now = clock.GetUtcNow();
        var existing = await db.Postmortems.FirstOrDefaultAsync(p => p.IncidentId == incidentId, ct);

        return existing is null
            ? await CreateAsync(incidentId, summary, rootCause, impact, correctiveActions, now, ct)
            : await UpdateAsync(existing, summary, rootCause, impact, correctiveActions, now, ct);
    }

    private async Task<SavePostmortemResult> CreateAsync(
        Guid incidentId, string summary, string rootCause, string impact, string correctiveActions,
        DateTimeOffset now, CancellationToken ct)
    {
        var postmortem = Postmortem.Create(
            incidentId, currentUser.UserId, summary, rootCause, impact, correctiveActions, now);

        db.Postmortems.Add(postmortem);
        AddActivity(incidentId, ActivityType.PostmortemCreated, "Postmortem created", postmortem.Id, now);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var alreadyExists = await db.Postmortems
                .AsNoTracking()
                .AnyAsync(p => p.IncidentId == incidentId, ct);

            if (alreadyExists)
                throw new ConflictException("A postmortem already exists for this incident. Please reload and try again.");

            throw;
        }

        return new SavePostmortemResult(await ToDtoAsync(postmortem, ct), Created: true);
    }

    private async Task<SavePostmortemResult> UpdateAsync(
        Postmortem existing, string summary, string rootCause, string impact, string correctiveActions,
        DateTimeOffset now, CancellationToken ct)
    {
        var changed = existing.Update(summary, rootCause, impact, correctiveActions, now);

        if (changed)
        {
            AddActivity(existing.IncidentId, ActivityType.PostmortemUpdated, "Postmortem updated", existing.Id, now);
            await db.SaveChangesAsync(ct);
        }

        return new SavePostmortemResult(await ToDtoAsync(existing, ct), Created: false);
    }

    private void AddActivity(Guid incidentId, ActivityType type, string message, Guid postmortemId, DateTimeOffset now)
    {
        db.IncidentActivities.Add(IncidentActivity.Create(
            incidentId,
            currentUser.UserId,                                  
            type,
            message,
            JsonSerializer.Serialize(new { postmortemId }),
            now));
    }

    private async Task<PostmortemDto> ToDtoAsync(Postmortem postmortem, CancellationToken ct)
    {
        var names = await identity.GetDisplayNamesAsync(new[] { postmortem.AuthorId }, ct);
        return postmortem.ToDto(names.GetValueOrDefault(postmortem.AuthorId));
    }
}