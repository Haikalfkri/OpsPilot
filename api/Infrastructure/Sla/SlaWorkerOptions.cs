using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Infrastructure.Sla
{
    public sealed class SlaWorkerOptions
    {
        public const string SectionName = "SlaWorker";

        public bool Enabled { get; set; } = true;

        public int IntervalSeconds { get; set; } = 60;
    }
}
