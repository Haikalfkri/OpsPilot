using OpsPilot.Domain.Sla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Application.Sla
{
    public interface ISlaService
    {
        SlaPolicyDto GetPolicy();
    }

    public sealed class SlaService(SlaPolicy policy) : ISlaService
    {
        public SlaPolicyDto GetPolicy() => policy.ToDto();
    }
}
