using OpsPilot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Postmortems
{
    public static class PostmortemMapper
    {
        public static PostmortemDto ToDto(this Postmortem postmortem, string? authorName) =>
            new(
                postmortem.Id,
                postmortem.IncidentId,
                postmortem.AuthorId,
                authorName,
                postmortem.Summary,
                postmortem.RootCause,
                postmortem.Impact,
                postmortem.CorrectiveActions,
                postmortem.CreatedAtUtc,
                postmortem.UpdatedAtUtc);
    }
}
