using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Domain.Entities
{
    public class Incident
    {
        public Guid Id { get; set; }
        public Guid TeamId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IncidentSeverity Severity { get; set; }
        public IncidentStatus Status { get; set; } = IncidentStatus.Open;

        public Guid CreatedById { get; set; }
        public Guid? AssigneeId { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public DateTimeOffset? FirstRespondedAtUtc { get; set; }
        public DateTimeOffset? ResolvedAtUtc { get; set; }
        public DateTimeOffset? ClosedAtUtc { get; set; }
        public DateTimeOffset ResponseDueAtUtc { get; set; }
        public DateTimeOffset ResolutionDueAtUtc { get; set; }

        public ICollection<IncidentComment> Comments { get; set; } = new List<IncidentComment>();
        public ICollection<IncidentActivity> Activities { get; set; } = new List<IncidentActivity>();
        public ICollection<IncidentSlaBreach> SlaBreaches { get; set; } = new List<IncidentSlaBreach>();
        public Postmortem? Postmortem { get; set; }
    }
}
