using ITAdmin.HostAgent.Contracts;

namespace ITAdmin.HostAgent;

/// <summary>
/// Who may ask the privileged agent for what.
///
/// <para>
/// The pipe ACL is the first gate and the one the kernel enforces: only the ITAdmin application
/// pool identity and local administrators can open the pipe at all. This class is the second gate,
/// and it exists because the two callers are not equivalent. An administrator at a console is
/// operating the machine directly. The web application is a network-facing service whose whole
/// purpose is to process untrusted input, so it gets the smallest set of operations that makes the
/// in-app update and settings experience work, and nothing that is only ever useful to an attacker.
/// </para>
///
/// <para>
/// Pure and string-based on purpose: the decision is unit-testable without Windows, a pipe, or a
/// service, which is what makes it possible to assert the boundary in CI rather than hoping.
/// </para>
/// </summary>
public sealed class HostAgentAuthorization
{
    /// <summary>
    /// Everything the web application may invoke. Note what is absent: nothing here can execute a
    /// command, read a file, or name a path.
    /// </summary>
    private static readonly HostAgentOperation[] WebApplicationOperations =
    [
        HostAgentOperation.Ping,
        HostAgentOperation.GetInstallationStatus,
        HostAgentOperation.CheckForUpdates,
        HostAgentOperation.RequestUpdate,
        HostAgentOperation.GetUpdateStatus,
        HostAgentOperation.RecycleApplicationPool,
        HostAgentOperation.GetHttpsStatus,
        HostAgentOperation.ConfigureHttps,
        HostAgentOperation.DisableHttps,
    ];

    private readonly string _appPoolIdentity;
    private readonly string? _appPoolSid;

    /// <param name="appPoolName">
    /// IIS application pool the site runs under. Its virtual account is
    /// <c>IIS APPPOOL\&lt;name&gt;</c>, which is what the pipe reports as the connecting principal.
    /// </param>
    /// <param name="appPoolSid">
    /// The SID of that virtual account, resolved once at start-up on Windows. When present it is the
    /// primary match: an impersonated token's <em>name</em> can come back in a form that does not
    /// string-equal <c>IIS APPPOOL\&lt;name&gt;</c> (localised group rendering, a translated SID, a
    /// bare account), but its SID is exact. The name match is kept as a fallback for hosts where the
    /// SID could not be resolved and for the off-Windows unit tests.
    /// </param>
    public HostAgentAuthorization(string appPoolName, string? appPoolSid = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appPoolName);
        _appPoolIdentity = "IIS APPPOOL\\" + appPoolName.Trim();
        _appPoolSid = string.IsNullOrWhiteSpace(appPoolSid) ? null : appPoolSid.Trim();
    }

    public string AppPoolIdentity => _appPoolIdentity;

    /// <summary>
    /// Decides an operation for a connected principal.
    ///
    /// <para>
    /// An unrecognised caller is denied outright rather than falling through to a default set. If a
    /// principal reaches this code that neither matches the app pool nor holds administrator
    /// rights, the pipe ACL has already been circumvented or misconfigured, and the correct
    /// response to "I do not know who you are" on a privileged channel is no.
    /// </para>
    /// </summary>
    public HostAgentAuthorizationDecision Authorize(
        string? callerIdentity,
        bool callerIsAdministrator,
        HostAgentOperation operation,
        string? callerSid = null)
    {
        if (!Enum.IsDefined(operation))
        {
            return HostAgentAuthorizationDecision.Deny("Unknown operation.");
        }

        if (callerIsAdministrator)
        {
            return HostAgentAuthorizationDecision.Allow();
        }

        if (string.IsNullOrWhiteSpace(callerIdentity) && string.IsNullOrWhiteSpace(callerSid))
        {
            return HostAgentAuthorizationDecision.Deny("The caller could not be identified.");
        }

        var matchesBySid = _appPoolSid is not null
            && !string.IsNullOrWhiteSpace(callerSid)
            && string.Equals(callerSid.Trim(), _appPoolSid, StringComparison.OrdinalIgnoreCase);

        var matchesByName = !string.IsNullOrWhiteSpace(callerIdentity)
            && string.Equals(callerIdentity.Trim(), _appPoolIdentity, StringComparison.OrdinalIgnoreCase);

        if (!matchesBySid && !matchesByName)
        {
            return HostAgentAuthorizationDecision.Deny("The caller is not permitted to use this service.");
        }

        return WebApplicationOperations.Contains(operation)
            ? HostAgentAuthorizationDecision.Allow()
            : HostAgentAuthorizationDecision.Deny(
                $"Operation {operation} is not available to the ITAdmin application.");
    }
}

public sealed record HostAgentAuthorizationDecision(bool IsAllowed, string Reason)
{
    public static HostAgentAuthorizationDecision Allow() => new(true, string.Empty);

    public static HostAgentAuthorizationDecision Deny(string reason) => new(false, reason);
}
