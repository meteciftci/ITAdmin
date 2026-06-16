using System.Net.Sockets;
using SasPortal.Application.Abstractions.Services;

namespace SasPortal.Infrastructure.Services;

public sealed class AdwsPortConnectivityChecker : IAdwsPortConnectivityChecker
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
        catch
        {
            return false;
        }
    }
}
