using System.Threading.RateLimiting;

namespace OpsPilot.Api.RateLimiting;

/// <summary>Pengaturan batas login, dibaca dari bagian "RateLimiting:Login" di appsettings.</summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "RateLimiting:Login";

    public int PermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
}

public static class RateLimitPolicies
{
    public const string Login = "login";
}

public static class LoginRateLimit
{
    public static RateLimitPartition<string> GetPartition(HttpContext context, LoginRateLimitOptions options) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = TimeSpan.FromSeconds(options.WindowSeconds),
                QueueLimit = 0, // melebihi batas = langsung ditolak, tidak antre
                AutoReplenishment = true
            });
}