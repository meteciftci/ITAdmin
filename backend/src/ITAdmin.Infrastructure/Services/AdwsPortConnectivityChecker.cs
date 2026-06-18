using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;

namespace ITAdmin.Infrastructure.Services;

public sealed class AdwsPortConnectivityChecker(ILogger<AdwsPortConnectivityChecker> logger) : IAdwsPortConnectivityChecker
{
    public async Task<bool> CanConnectAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0)
        {
            return false;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host.Trim(), port, timeoutSource.Token);
            await connectTask.ConfigureAwait(false);
            return client.Connected;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "ADWS port connectivity check failed. Host={Host} Port={Port}",
                host.Trim(),
                port);
            return false;
        }
    }
}
