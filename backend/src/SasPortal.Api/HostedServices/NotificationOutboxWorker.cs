using Microsoft.Extensions.Options;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Options;

namespace SasPortal.Api.HostedServices;

public sealed class NotificationOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOutboxOptions> options,
    ILogger<NotificationOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation("Notification outbox worker is disabled.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(
            options.Value.PollIntervalSeconds <= 0 ? 15 : options.Value.PollIntervalSeconds);

        logger.LogInformation(
            "Notification outbox worker started. Poll interval: {PollIntervalSeconds}s.",
            pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<INotificationOutboxBatchProcessor>();
                await processor.RecoverStaleProcessingAsync(stoppingToken);
                var processed = await processor.ProcessBatchAsync(stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("Processed {Count} notification outbox items.", processed);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Notification outbox worker iteration failed.");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
