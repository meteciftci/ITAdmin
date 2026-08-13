using ITAdmin.HostAgent.Contracts;

namespace ITAdmin.HostAgent;

/// <summary>
/// The one place a request crosses from "something a caller sent" to "something the machine does".
///
/// <para>
/// Order matters here and is fixed: parse, then validate shape, then authorize, then execute. A
/// request that fails any earlier stage never reaches the later ones, so an unauthorized caller
/// cannot use error differences to probe what the agent would have done. Execution itself is behind
/// <see cref="IHostAgentOperations"/> so the boundary can be tested with a fake that records what
/// it was asked to do without touching IIS, Git, or the filesystem.
/// </para>
/// </summary>
public sealed class HostAgentDispatcher(
    HostAgentAuthorization authorization,
    IHostAgentOperations operations)
{
    public async Task<HostAgentResponse> DispatchAsync(
        string? requestJson,
        HostAgentCallerContext caller,
        CancellationToken cancellationToken = default)
    {
        var request = HostAgentRequest.FromJson(requestJson);
        if (request is null)
        {
            return HostAgentResponse.Rejected("Request could not be parsed.");
        }

        var problems = request.Validate();
        if (problems.Count > 0)
        {
            return HostAgentResponse.Rejected(string.Join(" ", problems), request.CorrelationId);
        }

        var decision = authorization.Authorize(caller.Identity, caller.IsAdministrator, request.Operation);
        if (!decision.IsAllowed)
        {
            return HostAgentResponse.Denied(decision.Reason, request.CorrelationId);
        }

        try
        {
            return request.Operation switch
            {
                HostAgentOperation.Ping =>
                    HostAgentResponse.Ok("ITAdmin Host Agent is running.", request.CorrelationId),

                HostAgentOperation.GetInstallationStatus =>
                    await operations.GetInstallationStatusAsync(request, cancellationToken),

                HostAgentOperation.CheckForUpdates =>
                    await operations.CheckForUpdatesAsync(request, cancellationToken),

                HostAgentOperation.RequestUpdate =>
                    await operations.RequestUpdateAsync(request, cancellationToken),

                HostAgentOperation.GetUpdateStatus =>
                    await operations.GetUpdateStatusAsync(request, cancellationToken),

                HostAgentOperation.ReconcileWebBindings =>
                    await operations.ReconcileWebBindingsAsync(request, cancellationToken),

                HostAgentOperation.RecycleApplicationPool =>
                    await operations.RecycleApplicationPoolAsync(request, cancellationToken),

                _ => HostAgentResponse.Rejected("Unsupported operation.", request.CorrelationId),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The caller gets a stable message; the agent's own log gets the detail. An exception
            // from a privileged operation can name internal paths, the repository URL, or Git
            // transport internals, none of which belong in a response the web UI will render.
            operations.LogOperationFailure(request.Operation, exception);
            return HostAgentResponse.Failed(
                $"{request.Operation} failed on the host. See the ITAdmin Host Agent log.",
                request.CorrelationId);
        }
    }
}

/// <summary>Who is on the other end of the pipe, as determined by the transport.</summary>
public sealed record HostAgentCallerContext(string? Identity, bool IsAdministrator);

/// <summary>
/// The privileged operations themselves. Kept behind an interface so the dispatcher, the
/// authorization rules, and the protocol can all be tested off Windows.
/// </summary>
public interface IHostAgentOperations
{
    Task<HostAgentResponse> GetInstallationStatusAsync(HostAgentRequest request, CancellationToken cancellationToken);

    Task<HostAgentResponse> CheckForUpdatesAsync(HostAgentRequest request, CancellationToken cancellationToken);

    Task<HostAgentResponse> RequestUpdateAsync(HostAgentRequest request, CancellationToken cancellationToken);

    Task<HostAgentResponse> GetUpdateStatusAsync(HostAgentRequest request, CancellationToken cancellationToken);

    Task<HostAgentResponse> ReconcileWebBindingsAsync(HostAgentRequest request, CancellationToken cancellationToken);

    Task<HostAgentResponse> RecycleApplicationPoolAsync(HostAgentRequest request, CancellationToken cancellationToken);

    void LogOperationFailure(HostAgentOperation operation, Exception exception);

    /// <summary>
    /// Classifies whatever the previous run of the service left behind. Called once at start-up,
    /// before any request is accepted.
    /// </summary>
    void ReconcileInterruptedOperation();
}
