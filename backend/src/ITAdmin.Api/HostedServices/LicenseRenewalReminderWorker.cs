using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace ITAdmin.Api.HostedServices;

public sealed class LicenseRenewalReminderWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<LicenseRenewalReminderOptions> options,
    ILogger<LicenseRenewalReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation("License renewal reminder worker is disabled.");
            return;
        }

        var interval = TimeSpan.FromMinutes(
            options.Value.ScanIntervalMinutes <= 0 ? 360 : options.Value.ScanIntervalMinutes);
        logger.LogInformation("License renewal reminder worker started. Scan interval: {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ILicenseRenewalReminderProcessor>();
                var result = await processor.RunAsync(stoppingToken);
                logger.LogInformation(
                    "License renewal reminder scan completed. Due={Due} Queued={Queued} Existing={Existing} InvalidRecipients={Invalid}.",
                    result.DuePackageCount,
                    result.QueuedCount,
                    result.AlreadyQueuedCount,
                    result.InvalidRecipientCount);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "License renewal reminder scan failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
