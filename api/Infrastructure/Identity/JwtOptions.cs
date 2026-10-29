using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpsPilot.Infrastructure.Identity
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = "OpsPilot";
        public string Audience { get; set; } = "OpsPilot.Client";
        public string SigningKey { get; set; } = string.Empty;
        public int ExpiryMinutes { get; set; } = 60;
    }
}
