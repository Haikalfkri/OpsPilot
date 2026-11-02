using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Sla;

namespace OpsPilot.Infrastructure.Sla;

public sealed class SlaBreachWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaWorkerOptions> options,
    ILogger<SlaBreachWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.IntervalSeconds);
        logger.LogInformation("SLA worker started. Checking every {Interval}.", interval);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);   
            }
        }
        catch (OperationCanceledException)
        {
        }

        logger.LogInformation("SLA worker stopped.");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISlaBreachService>();

            var result = await service.DetectAndRecordAsync(ct);

            if (result.BreachesRecorded > 0)
                logger.LogInformation(
                    "SLA check: {Checked} incidents checked, {Breaches} breaches recorded, {Notifications} notifications created.",
                    result.IncidentsChecked, result.BreachesRecorded, result.NotificationsCreated);
            else
                logger.LogDebug("SLA check: {Checked} incidents checked, nothing new.", result.IncidentsChecked);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "SLA check failed. It will be retried on the next cycle.");
        }
    }
}