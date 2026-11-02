using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Notifications
{
    public sealed record NotificationDto(
        Guid Id,
        NotificationType Type,
        string Title,
        string Message,
        Guid? IncidentId,
        bool IsRead,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? ReadAtUtc);

    public sealed record ListNotificationsQuery(int Page = 1, int PageSize = 20, bool? IsRead = null);

    public sealed record UnreadCountDto(int Count);

    public sealed record MarkAllReadResultDto(int MarkedCount);
}
