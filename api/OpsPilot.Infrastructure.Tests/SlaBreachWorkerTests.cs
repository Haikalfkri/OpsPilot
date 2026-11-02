using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Sla;
using OpsPilot.Infrastructure.Sla;
using Xunit;

namespace OpsPilot.Infrastructure.Tests;

public class SlaBreachWorkerTests
{
    /// <summary>Service palsu: siklus pertama melempar error, siklus berikutnya berhasil.</summary>
    private sealed class FlakyBreachService : ISlaBreachService
    {
        public int Calls;

        public Task<SlaBreachRunResult> DetectAndRecordAsync(CancellationToken ct)
        {
            var call = Interlocked.Increment(ref Calls);

            if (call == 1)
                throw new InvalidOperationException("Simulated failure in the first cycle.");

            return Task.FromResult(new SlaBreachRunResult(0, 0, 0));
        }
    }

    [Fact]
    public async Task Worker_KeepsRunning_EvenWhenOneCycleThrows()
    {
        var service = new FlakyBreachService();
        var provider = new ServiceCollection()
            .AddSingleton<ISlaBreachService>(service)
            .BuildServiceProvider();

        var worker = new SlaBreachWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SlaWorkerOptions { IntervalSeconds = 1 }),
            NullLogger<SlaBreachWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        // Tunggu sampai worker sudah menjalankan minimal 2 siklus (maksimal 10 detik)
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (service.Calls < 2 && DateTime.UtcNow < deadline)
            await Task.Delay(100);

        await worker.StopAsync(CancellationToken.None);

        // Siklus 1 gagal, tetapi siklus 2 tetap berjalan: worker tidak mati
        Assert.True(service.Calls >= 2, $"Worker stopped after {service.Calls} call(s).");
    }
}