using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Sla
{
    public static class SlaMapper
    {
        private const string Note =
            "These targets are configurable demo examples (appsettings, section \"Sla\"), not an industry standard.)";

        public static SlaPolicyDto ToDto(this SlaPolicy policy)
        {
            var targets = Enum.GetValues<IncidentSeverity>()
                .OrderByDescending(severity => (int)severity)
                .Select(severity =>
                {
                    var t = policy.GetTargets(severity);
                    return new SlaTargetDto(severity, (int)t.ResponseTarget.TotalMinutes, (int)t.ResolutionTarget.TotalMinutes);
                })
                .ToList();

            return new SlaPolicyDto(targets, Note);
        }
    }
}
