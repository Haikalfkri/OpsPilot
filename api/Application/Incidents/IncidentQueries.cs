using OpsPilot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Incidents
{
    public sealed record ListIncidentsQuery(
        int Page = 1,
        int PageSize = 20,
        IncidentStatus? Status = null,
        IncidentSeverity? Severity = null,
        Guid? AssigneeId = null,
        DateTimeOffset? CreatedFrom = null,
        DateTimeOffset? CreatedTo = null,
        string? Search = null,
        string SortBy = IncidentSortFields.CreatedAt,
        string SortDirection = "desc");

    public static class IncidentSortFields
    {
        public const string CreatedAt = "createdAt";
        public const string UpdatedAt = "updatedAt";
        public const string Severity = "severity";
        public const string DueAt = "dueAt";      // batas waktu penyelesaian (SLA)

        public static readonly string[] All = { CreatedAt, UpdatedAt, Severity, DueAt };
    }
}
