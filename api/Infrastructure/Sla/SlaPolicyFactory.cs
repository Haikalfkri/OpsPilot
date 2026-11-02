using Microsoft.Extensions.Configuration;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Infrastructure.Sla
{
    public static class SlaPolicyFactory
    {
        public static SlaPolicy Create(IConfigurationSection section)
        {
            if (!section.Exists())
            {
                return SlaPolicy.Default();
            }

            var targets = new Dictionary<IncidentSeverity, SlaTargets>();

            // periksa keempat severity satu per satu
            foreach (var severity in Enum.GetValues<IncidentSeverity>())
            {
                var item = section.GetSection(severity.ToString());

                var responseMinutes = item.GetValue<int?>("ResponseMinutes");
                var resolutionMinutes = item.GetValue<int?>("ResolutionMinutes");

                if (responseMinutes is null || resolutionMinutes is null)
                {
                    throw new InvalidOperationException(
                        $"Configuration Sla:{severity} musd define ResponseMinutes and ResolutionMinutes.");
                }

                targets[severity] = new SlaTargets(
                TimeSpan.FromMinutes(responseMinutes.Value),
                TimeSpan.FromMinutes(resolutionMinutes.Value));
            }

            return new SlaPolicy(targets);
        }
    }
}
