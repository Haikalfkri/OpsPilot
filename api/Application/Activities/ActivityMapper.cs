using System.Linq.Expressions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Activities;

public static class ActivityMapper
{
    public static readonly Expression<Func<IncidentActivity, ActivityDto>> Projection =
        a => new ActivityDto(a.Id, a.Type, a.Message, a.ActorId, null, a.CreatedAtUtc);
}