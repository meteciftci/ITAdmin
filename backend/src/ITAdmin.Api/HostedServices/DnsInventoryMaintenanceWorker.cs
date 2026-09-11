using ITAdmin.Application.Abstractions.Services;

namespace ITAdmin.Api.HostedServices;

public sealed class DnsInventoryMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DnsInventoryMaintenanceWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SchedulerInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DNS inventory maintenance worker started.");
        var nextCleanupAt = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IDnsInventorySyncService>();
                var queued = await service.EnqueueDueAutomaticAsync(now, stoppingToken);
                if (queued > 0)
                    logger.LogInformation("Queued {Count} automatic DNS inventory synchronizations.", queued);

                if (now >= nextCleanupAt)
                {
                    var purged = await service.PurgeExpiredSnapshotsAsync(now, stoppingToken);
                    if (purged > 0)
                        logger.LogInformation("Purged {Count} expired DNS inventory snapshots.", purged);
                    nextCleanupAt = now + CleanupInterval;
                }

                await Task.Delay(SchedulerInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "DNS inventory maintenance worker iteration failed.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
