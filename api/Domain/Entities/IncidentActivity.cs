using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Domain.Entities
{
    public class IncidentActivity
    {
        public Guid Id { get; set; }
        public Guid IncidentId { get; set; }
        public Incident Incident { get; set; } = null!;
        public Guid? ActorId { get; set; }          // null = dilakukan oleh sistem (misalnya SLA worker)
        public ActivityType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }

        public static IncidentActivity Create(
            Guid incidentId, Guid? actorId, ActivityType type, string message, string? metadataJson, DateTimeOffset now) =>
            new()
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                ActorId = actorId,
                Type = type,
                Message = message,
                MetadataJson = metadataJson,
                CreatedAtUtc = now
            };


    }
}
