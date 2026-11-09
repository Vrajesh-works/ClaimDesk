using ClaimDesk.Application.Sla;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimDesk.Infrastructure.Sla;

/// <summary>Background service that runs the SLA scan on a fixed interval.</summary>
public sealed class SlaWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaOptions> options,
    TimeProvider time,
    ILogger<SlaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA worker started; scanning every {Interval}", options.Value.ScanInterval);
        using var timer = new PeriodicTimer(options.Value.ScanInterval, time);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var scanner = scope.ServiceProvider.GetRequiredService<SlaScanner>();
            var result = await scanner.ScanAsync(ct);
            if (result.Reminders + result.StuckAlerts > 0)
            {
                logger.LogInformation(
                    "SLA scan raised {Reminders} reminders and {Stuck} stuck-claim alerts",
                    result.Reminders,
                    result.StuckAlerts);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed scan must not kill the worker; the next tick retries.
            logger.LogError(ex, "SLA scan failed");
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
