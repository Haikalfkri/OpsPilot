using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Activities
{
    public sealed record ActivityDto(
        Guid Id,
        ActivityType Type,
        string Message,
        Guid? ActorId,          // null kalau dilakukan sistem
        string? ActorName,      // "System" kalau dilakukan sistem
        DateTimeOffset CreatedAtUtc);
}
