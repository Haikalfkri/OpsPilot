using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Domain.Entities
{
    public class IncidentSlaBreach
    {
        public Guid Id { get; set; }
        public Guid IncidentId { get; set; }
        public Incident Incident { get; set; } = null!;
        public SlaBreachType BreachType { get; set; }
        public DateTimeOffset BreachedAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
    }
}
