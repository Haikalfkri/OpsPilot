using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Extensions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Comments;

public interface ICommentService
{
    Task<PagedResult<CommentDto>> ListAsync(Guid incidentId, PageQuery query, CancellationToken ct);
    Task<CommentDto> AddAsync(Guid incidentId, CreateCommentRequest request, CancellationToken ct);
}

public sealed class CommentService(
    IValidator<PageQuery> pageValidator,
    IValidator<CreateCommentRequest> createValidator,
    IApplicationDbContext db,
    IIdentityService identity,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommentService
{
    public async Task<PagedResult<CommentDto>> ListAsync(Guid incidentId, PageQuery query, CancellationToken ct)
    {
        await pageValidator.ValidateAndThrowAsync(query, ct);
        await db.EnsureIncidentInTeamAsync(incidentId, currentUser.TeamId, ct);

        var comments = db.IncidentComments
            .AsNoTracking()
            .Where(c => c.IncidentId == incidentId);

        var totalCount = await comments.CountAsync(ct);

        var items = await comments
            .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(CommentMapper.Projection)
            .ToListAsync(ct);

        var authorIds = items.Select(i => i.AuthorId).Distinct().ToList();
        var names = await identity.GetDisplayNamesAsync(authorIds, ct);

        var withNames = items
            .Select(i => i with { AuthorName = names.GetValueOrDefault(i.AuthorId) })
            .ToList();

        return new PagedResult<CommentDto>(withNames, query.Page, query.PageSize, totalCount);
    }

    public async Task<CommentDto> AddAsync(Guid incidentId, CreateCommentRequest request, CancellationToken ct)
    {
        if (currentUser.Role is not (UserRole.Admin or UserRole.Responder))
            throw new ForbiddenException();

        await createValidator.ValidateAndThrowAsync(request, ct);
        await db.EnsureIncidentInTeamAsync(incidentId, currentUser.TeamId, ct);

        var now = clock.GetUtcNow();

        var comment = IncidentComment.Create(incidentId, currentUser.UserId, request.Body.Trim(), now);
        db.IncidentComments.Add(comment);

        db.IncidentActivities.Add(IncidentActivity.Create(
            incidentId,
            currentUser.UserId,
            ActivityType.CommentAdded,
            "Comment added",
            JsonSerializer.Serialize(new { commentId = comment.Id }),
            now));

        await db.SaveChangesAsync(ct);

        var names = await identity.GetDisplayNamesAsync(new[] { currentUser.UserId }, ct);
        return comment.ToDto(names.GetValueOrDefault(currentUser.UserId));
    }
}