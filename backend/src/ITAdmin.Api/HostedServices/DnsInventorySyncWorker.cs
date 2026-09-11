using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Api.HostedServices;

public sealed class DnsInventorySyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DnsInventorySyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DNS inventory synchronization worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var parallelism = await ReadParallelismAsync(stoppingToken);
                var work = await Task.WhenAll(Enumerable.Range(0, parallelism)
                    .Select(_ => ProcessOneAsync(stoppingToken)));
                await Task.Delay(work.Any(x => x) ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "DNS inventory synchronization worker iteration failed.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task<int> ReadParallelismAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return Math.Clamp(await context.DnsManagementSettings.AsNoTracking()
            .Select(x => (int?)x.MaxParallelServers).SingleOrDefaultAsync(cancellationToken) ?? 1, 1, 20);
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IDnsInventorySyncService>();
        return await processor.ProcessNextAsync(cancellationToken);
    }
}
