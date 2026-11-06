using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Postmortems
{
    public sealed record SavePostmortemRequest(
        string Summary,
        string RootCause,
        string Impact,
        string CorrectiveActions);

    public sealed record PostmortemDto(
        Guid Id,
        Guid IncidentId,
        Guid AuthorId,
        string? AuthorName,
        string Summary,
        string RootCause,
        string Impact,
        string CorrectiveActions,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    public sealed record SavePostmortemResult(PostmortemDto Postmortem, bool Created);
}
