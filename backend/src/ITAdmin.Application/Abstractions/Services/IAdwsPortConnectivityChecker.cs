namespace ITAdmin.Application.Abstractions.Services;

public interface IAdwsPortConnectivityChecker
{
    Task<bool> CanConnectAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default);
}
