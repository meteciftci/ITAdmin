using ITAdmin.HostAgent.Contracts;

namespace ITAdmin.Api.HostAgent;

public interface IHostAgentClient
{
    Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default);
}

public sealed class HostAgentUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
