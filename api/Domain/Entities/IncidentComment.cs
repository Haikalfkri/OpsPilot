using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Domain.Entities
{
    public class IncidentComment
    {
        public Guid Id { get; set; }
        public Guid IncidentId { get; set; }
        public Incident Incident { get; set; } = null!;
        public Guid AuthorId { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }

        public static IncidentComment Create(Guid incidentId, Guid authorId, string body, DateTimeOffset now) =>
            new()
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                AuthorId = authorId,
                Body = body,
                CreatedAtUtc = now
            };
    }
}
