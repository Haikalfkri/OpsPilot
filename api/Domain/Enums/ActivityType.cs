using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Domain.Enums
{
    public enum ActivityType
    {
        Created, Updated, SeverityChanged, StatusChanged, AssigneeChanged, CommentAdded,
        Resolved, Closed, ResponseSlaBreached, ResolutionSlaBreached, PostmortemCreated, PostmortemUpdated
    }
}
