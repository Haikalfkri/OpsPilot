using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Notifications
{
    public interface INotificationService
    {
        Task<PagedResult<NotificationDto>> ListAsync(ListNotificationsQuery query, CancellationToken ct);
        Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct);
        Task<NotificationDto> MarkReadAsync(Guid id, CancellationToken ct);
        Task<MarkAllReadResultDto> MarkAllReadAsync(CancellationToken ct);
    }

    public sealed class NotificationService(
        IValidator<ListNotificationsQuery> validator,
        IApplicationDbContext db,
        ICurrentUser currentUser,
        TimeProvider clock) : INotificationService
    {
        public async Task<PagedResult<NotificationDto>> ListAsync(ListNotificationsQuery query, CancellationToken ct)
        {
            await validator.ValidateAndThrowAsync(query, ct);

            var myId = currentUser.UserId;   // dari token, bukan dari request

            var q = db.Notifications.AsNoTracking().Where(n => n.RecipientId == myId);

            // Filter opsional: hanya yang sudah / belum dibaca
            if (query.IsRead is { } isRead)
                q = q.Where(n => n.IsRead == isRead);

            var totalCount = await q.CountAsync(ct);

            var items = await q
                .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(NotificationMapper.Projection)
                .ToListAsync(ct);

            return new PagedResult<NotificationDto>(items, query.Page, query.PageSize, totalCount);
        }

        public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct)
        {
            var myId = currentUser.UserId;

            var count = await db.Notifications
                .AsNoTracking()
                .CountAsync(n => n.RecipientId == myId && !n.IsRead, ct);

            return new UnreadCountDto(count);
        }

        public async Task<NotificationDto> MarkReadAsync(Guid id, CancellationToken ct)
        {
            var myId = currentUser.UserId;

            var notification = await db.Notifications
                                   .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == myId, ct)
                               ?? throw new NotFoundException("Notification not found.");

            if (!notification.IsRead)
            {
                notification.MarkAsRead(clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
            }

            return notification.ToDto();
        }

        public async Task<MarkAllReadResultDto> MarkAllReadAsync(CancellationToken ct)
        {
            var myId = currentUser.UserId;
            var now = clock.GetUtcNow();

            // Hanya milik saya yang belum dibaca
            var unread = await db.Notifications
                .Where(n => n.RecipientId == myId && !n.IsRead)
                .ToListAsync(ct);

            foreach (var notification in unread)
                notification.MarkAsRead(now);

            await db.SaveChangesAsync(ct);

            return new MarkAllReadResultDto(unread.Count);
        }
    }
}
