using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Domain.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }
        public Guid RecipientId { get; set; }
        public Guid? IncidentId { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? ReadAtUtc { get; set; }

        public static Notification Create(
            Guid recipientId, Guid? incidentId, NotificationType type, string title, string message, DateTimeOffset now) =>
            new()
            {
                Id = Guid.NewGuid(),
                RecipientId = recipientId,
                IncidentId = incidentId,
                Type = type,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAtUtc = now
            };

        public void MarkAsRead(DateTimeOffset now)
        {
            if (IsRead)
                return;

            IsRead = true;
            ReadAtUtc = now;
        }
    }
}
