using OpsPilot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Notifications
{
    public static class NotificationMapper
    {
        public static readonly Expression<Func<Notification, NotificationDto>> Projection =
            n => new NotificationDto(
                n.Id, n.Type, n.Title, n.Message, n.IncidentId, n.IsRead, n.CreatedAtUtc, n.ReadAtUtc);

        public static NotificationDto ToDto(this Notification n) =>
            new(n.Id, n.Type, n.Title, n.Message, n.IncidentId, n.IsRead, n.CreatedAtUtc, n.ReadAtUtc);
    }
}
