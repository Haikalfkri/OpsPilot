using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using OpsPilot.Api.RateLimiting;
using Xunit;

namespace OpsPilot.Api.IntegrationTests;

public class LoginRateLimitTests
{
    private static HttpContext Request(string? ip)
    {
        var context = new DefaultHttpContext();
        if (ip is not null) context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    private static PartitionedRateLimiter<HttpContext> CreateLimiter(int permitLimit, int windowSeconds = 60) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            LoginRateLimit.GetPartition(context, new LoginRateLimitOptions
            {
                PermitLimit = permitLimit,
                WindowSeconds = windowSeconds
            }));

    [Fact]
    public void AllowsExactlyTheConfiguredNumberOfAttempts_ThenRejects()
    {
        using var limiter = CreateLimiter(permitLimit: 3);
        var context = Request("10.0.0.1");

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var lease = limiter.AttemptAcquire(context);
            Assert.True(lease.IsAcquired, $"Attempt {attempt} should be allowed");
        }

        using var rejected = limiter.AttemptAcquire(context);
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public void EachIpAddressHasItsOwnBudget()
    {
        using var limiter = CreateLimiter(permitLimit: 1);

        using var first = limiter.AttemptAcquire(Request("10.0.0.1"));
        using var secondSameIp = limiter.AttemptAcquire(Request("10.0.0.1"));
        using var otherIp = limiter.AttemptAcquire(Request("10.0.0.2"));

        Assert.True(first.IsAcquired);
        Assert.False(secondSameIp.IsAcquired);
        Assert.True(otherIp.IsAcquired);   // IP lain tidak terkena batas IP pertama
    }

    [Fact]
    public void RequestsWithoutAKnownAddress_ShareOneBudget()
    {
        using var limiter = CreateLimiter(permitLimit: 1);

        using var first = limiter.AttemptAcquire(Request(null));
        using var second = limiter.AttemptAcquire(Request(null));

        Assert.True(first.IsAcquired);
        Assert.False(second.IsAcquired);
    }

    [Fact]
    public void ARejection_TellsTheClientHowLongToWait()
    {
        using var limiter = CreateLimiter(permitLimit: 1, windowSeconds: 60);
        var context = Request("10.0.0.1");

        using var allowed = limiter.AttemptAcquire(context);
        using var rejected = limiter.AttemptAcquire(context);

        Assert.True(rejected.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.InRange(retryAfter, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task TheBudgetComesBackAfterTheWindow()
    {
        using var limiter = CreateLimiter(permitLimit: 1, windowSeconds: 1);
        var context = Request("10.0.0.1");

        using var first = limiter.AttemptAcquire(context);
        using var blocked = limiter.AttemptAcquire(context);
        Assert.True(first.IsAcquired);
        Assert.False(blocked.IsAcquired);

        await Task.Delay(TimeSpan.FromMilliseconds(2200));   // lebih dari dua jendela

        using var again = limiter.AttemptAcquire(context);
        Assert.True(again.IsAcquired);
    }
}