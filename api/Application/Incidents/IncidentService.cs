using System.Linq.Expressions;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;

namespace OpsPilot.Application.Incidents;

public interface IIncidentService
{
    Task<PagedResult<IncidentSummaryDto>> ListAsync(ListIncidentsQuery query, CancellationToken ct);
    Task<IncidentDetailDto> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IncidentDetailDto> CreateAsync(CreateIncidentRequest request, CancellationToken ct);
    Task<IncidentDetailDto> UpdateAsync(Guid id, UpdateIncidentRequest request, CancellationToken ct);
    Task<IncidentDetailDto> ChangeStatusAsync(Guid id, ChangeStatusRequest request, CancellationToken ct);
    Task<IncidentDetailDto> AssignAsync(Guid id, AssignIncidentRequest request, CancellationToken ct);
}

public sealed class IncidentService(
    IValidator<ListIncidentsQuery> listValidator,
    IValidator<CreateIncidentRequest> createValidator,
    IValidator<UpdateIncidentRequest> updateValidator,
    IValidator<ChangeStatusRequest> statusValidator,
    IApplicationDbContext db,
    IIdentityService identity,
    ICurrentUser currentUser,
    SlaPolicy sla,
    TimeProvider clock) : IIncidentService
{
    // Peringkat severity untuk sorting. Severity disimpan sebagai TEKS di database,
    // jadi kalau diurutkan langsung hasilnya alfabetis (Critical, High, Low, Medium) yang salah.
    // Ekspresi ini mengubahnya jadi angka: Critical=3, High=2, Medium=1, Low=0.
    private static readonly Expression<Func<Incident, int>> SeverityRank = i =>
        i.Severity == IncidentSeverity.Critical ? 3 :
        i.Severity == IncidentSeverity.High ? 2 :
        i.Severity == IncidentSeverity.Medium ? 1 : 0;

    public async Task<PagedResult<IncidentSummaryDto>> ListAsync(ListIncidentsQuery query, CancellationToken ct)
    {
        await listValidator.ValidateAndThrowAsync(query, ct);

        var teamId = currentUser.TeamId;

        // Mulai dari incident di TIM SAYA (tim dari token). Ini kunci isolasi tim.
        var q = db.Incidents.AsNoTracking().Where(i => i.TeamId == teamId);

        // Tambahkan filter hanya kalau dikirim client
        if (query.Status is { } status)
            q = q.Where(i => i.Status == status);

        if (query.Severity is { } severity)
            q = q.Where(i => i.Severity == severity);

        if (query.AssigneeId is { } assigneeId)
            q = q.Where(i => i.AssigneeId == assigneeId);

        if (query.CreatedFrom is { } createdFrom)
            q = q.Where(i => i.CreatedAtUtc >= createdFrom);

        if (query.CreatedTo is { } createdTo)
            q = q.Where(i => i.CreatedAtUtc <= createdTo);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(i => i.Title.ToLower().Contains(term) || i.Description.ToLower().Contains(term));
        }

        // Hitung total (untuk paging), lalu ambil satu halaman saja
        var totalCount = await q.CountAsync(ct);

        var items = await ApplySorting(q, query.SortBy, query.SortDirection)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(IncidentMapper.SummaryProjection)
            .ToListAsync(ct);

        // Lengkapi nama assignee dengan SATU query tambahan (bukan satu per baris)
        var names = await GetNamesAsync(items.Select(i => i.AssigneeId), ct);

        var withNames = items
            .Select(i => i with { AssigneeName = FindName(i.AssigneeId, names) })
            .ToList();

        return new PagedResult<IncidentSummaryDto>(withNames, query.Page, query.PageSize, totalCount);
    }

    public async Task<IncidentDetailDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var incident = await FindIncidentAsync(id, track: false, ct);
        return await ToDetailAsync(incident, ct);
    }

    public async Task<IncidentDetailDto> CreateAsync(CreateIncidentRequest request, CancellationToken ct)
    {
        EnsureCanWrite();
        await createValidator.ValidateAndThrowAsync(request, ct);

        if (request.AssigneeId is { } assigneeId)
            await EnsureValidAssigneeAsync(assigneeId, ct);

        var now = clock.GetUtcNow();

        var incident = Incident.Create(
            currentUser.TeamId,
            currentUser.UserId,
            request.Title.Trim(),
            request.Description.Trim(),
            request.Severity!.Value,
            request.AssigneeId,
            sla,
            now);

        db.Incidents.Add(incident);

        AddActivity(incident.Id, ActivityType.Created, "Incident created",
            new { severity = incident.Severity.ToString() }, now);

        if (incident.AssigneeId is { } assigned)
        {
            var names = await GetNamesAsync(new Guid?[] { assigned }, ct);

            AddActivity(incident.Id, ActivityType.AssigneeChanged,
                $"Assigned to {DisplayName(assigned, names)}",
                new { from = (Guid?)null, to = assigned }, now);
        }

        // SATU SaveChanges = incident + activity tersimpan bersamaan, atau tidak sama sekali.
        await db.SaveChangesAsync(ct);

        return await ToDetailAsync(incident, ct);
    }

    public async Task<IncidentDetailDto> UpdateAsync(Guid id, UpdateIncidentRequest request, CancellationToken ct)
    {
        EnsureCanWrite();
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var incident = await FindIncidentAsync(id, track: true, ct);
        var now = clock.GetUtcNow();

        // ---- Judul / deskripsi ----
        var newTitle = request.Title?.Trim();
        var newDescription = request.Description?.Trim();

        var changedFields = new List<string>();
        if (newTitle is not null && newTitle != incident.Title) changedFields.Add("title");
        if (newDescription is not null && newDescription != incident.Description) changedFields.Add("description");

        if (changedFields.Count > 0)
        {
            incident.UpdateDetails(newTitle ?? incident.Title, newDescription ?? incident.Description, now);

            AddActivity(incident.Id, ActivityType.Updated, "Incident details updated",
                new { fields = changedFields }, now);
        }

        // ---- Severity ----
        if (request.Severity is { } newSeverity && newSeverity != incident.Severity)
        {
            var oldSeverity = incident.Severity;
            incident.ChangeSeverity(newSeverity, sla, now);   // juga menghitung ulang batas SLA

            AddActivity(incident.Id, ActivityType.SeverityChanged,
                $"Severity changed from {oldSeverity} to {newSeverity}",
                new { from = oldSeverity.ToString(), to = newSeverity.ToString() }, now);
        }

        // Kalau tidak ada yang benar-benar berubah, SaveChanges tidak menulis apa pun.
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(incident, ct);
    }

    public async Task<IncidentDetailDto> ChangeStatusAsync(Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        EnsureCanWrite();
        await statusValidator.ValidateAndThrowAsync(request, ct);

        var incident = await FindIncidentAsync(id, track: true, ct);
        var now = clock.GetUtcNow();

        var from = incident.Status;
        var to = request.Status!.Value;

        // Aturan perpindahan ada di Domain. Kalau tidak sah, DomainRuleException (-> 409).
        incident.ChangeStatus(to, now);

        // Satu perubahan = satu activity. Resolved dan Closed punya tipe sendiri.
        var activityType = to switch
        {
            IncidentStatus.Resolved => ActivityType.Resolved,
            IncidentStatus.Closed => ActivityType.Closed,
            _ => ActivityType.StatusChanged
        };

        var message = to switch
        {
            IncidentStatus.Resolved => "Incident resolved",
            IncidentStatus.Closed => "Incident closed",
            _ => $"Status changed from {from} to {to}"
        };

        AddActivity(incident.Id, activityType, message,
            new { from = from.ToString(), to = to.ToString() }, now);

        await db.SaveChangesAsync(ct);

        // Phase 8: hapus cache dashboard tim ini di sini.
        return await ToDetailAsync(incident, ct);
    }

    public async Task<IncidentDetailDto> AssignAsync(Guid id, AssignIncidentRequest request, CancellationToken ct)
    {
        EnsureCanWrite();

        var incident = await FindIncidentAsync(id, track: true, ct);
        var newAssigneeId = request.AssigneeId;

        // Assignee tidak berubah? Kembalikan apa adanya, tanpa activity ganda.
        if (incident.AssigneeId == newAssigneeId)
            return await ToDetailAsync(incident, ct);

        if (newAssigneeId is { } assigneeId)
            await EnsureValidAssigneeAsync(assigneeId, ct);

        var now = clock.GetUtcNow();
        var oldAssigneeId = incident.AssigneeId;

        incident.AssignTo(newAssigneeId, now);

        var names = await GetNamesAsync(new Guid?[] { oldAssigneeId, newAssigneeId }, ct);

        AddActivity(incident.Id, ActivityType.AssigneeChanged,
            $"Assignee changed from {DisplayName(oldAssigneeId, names)} to {DisplayName(newAssigneeId, names)}",
            new { from = oldAssigneeId, to = newAssigneeId }, now);

        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(incident, ct);
    }

    private void EnsureCanWrite()
    {
        if (currentUser.Role is not (UserRole.Admin or UserRole.Responder))
            throw new ForbiddenException();
    }

    private async Task<Incident> FindIncidentAsync(Guid id, bool track, CancellationToken ct)
    {
        var teamId = currentUser.TeamId;

        var q = db.Incidents.Where(i => i.Id == id && i.TeamId == teamId);
        if (!track)
            q = q.AsNoTracking();   // hanya baca: lebih cepat

        return await q.FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("Incident not found.");
    }

    private async Task EnsureValidAssigneeAsync(Guid assigneeId, CancellationToken ct)
    {
        var user = await identity.GetByIdAsync(assigneeId, ct);

        var isValid = user is not null
                      && user.IsActive
                      && user.TeamId == currentUser.TeamId
                      && user.Role != UserRole.Viewer;

        if (!isValid)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("AssigneeId", "Assignee must be an active Admin or Responder of your team.")
            });
        }
    }

    private void AddActivity(Guid incidentId, ActivityType type, string message, object? metadata, DateTimeOffset now)
    {
        db.IncidentActivities.Add(new IncidentActivity
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            ActorId = currentUser.UserId,
            Type = type,
            Message = message,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
            CreatedAtUtc = now
        });
    }

    private static IOrderedQueryable<Incident> ApplySorting(IQueryable<Incident> q, string sortBy, string sortDirection)
    {
        var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var sorted = sortBy.ToLowerInvariant() switch
        {
            "updatedat" => desc ? q.OrderByDescending(i => i.UpdatedAtUtc) : q.OrderBy(i => i.UpdatedAtUtc),
            "severity" => desc ? q.OrderByDescending(SeverityRank) : q.OrderBy(SeverityRank),
            "dueat" => desc ? q.OrderByDescending(i => i.ResolutionDueAtUtc) : q.OrderBy(i => i.ResolutionDueAtUtc),
            _ => desc ? q.OrderByDescending(i => i.CreatedAtUtc) : q.OrderBy(i => i.CreatedAtUtc)
        };

        return sorted.ThenBy(i => i.Id);
    }
    private async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid?> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await identity.GetDisplayNamesAsync(ids, ct);
    }

    private static string? FindName(Guid? userId, IReadOnlyDictionary<Guid, string> names) =>
        userId is { } id && names.TryGetValue(id, out var name) ? name : null;

    private static string DisplayName(Guid? userId, IReadOnlyDictionary<Guid, string> names)
    {
        if (userId is null) return "Unassigned";
        return names.TryGetValue(userId.Value, out var name) ? name : "Unknown user";
    }

    private async Task<IncidentDetailDto> ToDetailAsync(Incident incident, CancellationToken ct)
    {
        var names = await GetNamesAsync(new Guid?[] { incident.CreatedById, incident.AssigneeId }, ct);

        return incident.ToDetailDto(
            FindName(incident.CreatedById, names),
            FindName(incident.AssigneeId, names));
    }
}