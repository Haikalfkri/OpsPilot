using OpsPilot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Comments
{
    public static class CommentMapper
    {
        public static readonly Expression<Func<IncidentComment, CommentDto>> Projection =
            c => new CommentDto(c.Id, c.IncidentId, c.AuthorId, null, c.Body, c.CreatedAtUtc);

        public static CommentDto ToDto(this IncidentComment comment, string? authorName) =>
            new(comment.Id, comment.IncidentId, comment.AuthorId, authorName, comment.Body, comment.CreatedAtUtc);
    }
}
