using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Comments
{
    public sealed record CreateCommentRequest(string Body);

    public sealed record CommentDto(
        Guid Id,
        Guid IncidentId,
        Guid AuthorId,
        string? AuthorName,
        string Body,
        DateTimeOffset CreatedAtUtc);


}
