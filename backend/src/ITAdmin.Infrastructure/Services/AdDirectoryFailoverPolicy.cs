using System.DirectoryServices.Protocols;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

/// <summary>
/// Ordered domain-controller failover policy shared by the runtime directory operations
/// (<see cref="AdDirectoryServiceBase"/>) and by connection validation
/// (<see cref="AdManagementValidationService"/>) so both behave identically.
///
/// The rule is: a bind failure that is specific to one endpoint (host down, TCP/TLS refused,
/// expired or mis-issued certificate on that DC) moves on to the next preferred controller,
/// because a sibling DC can still serve the request. A failure that the whole domain would
/// return the same way (rejected credentials, auth policy, access rights) stops immediately —
/// retrying it against every DC only multiplies lockout risk and request latency.
/// </summary>
internal static class AdDirectoryFailoverPolicy
{
    /// <summary>LDAP_AUTH_METHOD_NOT_SUPPORTED (7).</summary>
    private const int AuthMethodNotSupported = 7;

    /// <summary>LDAP_STRONG_AUTH_REQUIRED (8).</summary>
    private const int StrongAuthRequired = 8;

    /// <summary>LDAP_CONFIDENTIALITY_REQUIRED (13).</summary>
    private const int ConfidentialityRequired = 13;

    /// <summary>LDAP_INAPPROPRIATE_AUTH (48).</summary>
    private const int InappropriateAuthentication = 48;

    /// <summary>LDAP_INVALID_CREDENTIALS (49).</summary>
    private const int InvalidCredentials = 49;

    /// <summary>LDAP_INSUFFICIENT_ACCESS (50).</summary>
    private const int InsufficientAccessRights = 50;

    /// <summary>LDAP_SERVER_DOWN (81).</summary>
    private const int LdapServerDown = 81;

    /// <summary>LDAP_TIMEOUT (85).</summary>
    private const int LdapTimeout = 85;

    /// <summary>LDAP_CONNECT_ERROR (91) — also how Schannel surfaces LDAPS certificate rejection.</summary>
    private const int LdapConnectError = 91;

    /// <summary>LDAP_BUSY (51).</summary>
    private const int LdapBusy = 51;

    /// <summary>LDAP_UNAVAILABLE (52).</summary>
    private const int LdapUnavailable = 52;

    /// <summary>
    /// Preferred domain controllers in administrator-defined order, de-duplicated, with the
    /// domain FQDN appended last so DNS-based DC locator stays available as a final fallback.
    /// The list is recomputed per operation and holds no sticky state, so once the first
    /// controller recovers the very next operation starts from it again.
    /// </summary>
    internal static IReadOnlyList<string> ResolveOrderedHosts(AdManagementConnectionParameters connection)
    {
        var hosts = connection.PreferredDomainControllers
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(host => host.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(connection.DomainFqdn)
            && !hosts.Contains(connection.DomainFqdn.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            hosts.Add(connection.DomainFqdn.Trim());
        }

        return hosts;
    }

    /// <summary>
    /// True when the failure is attributable to this one endpoint and the next preferred
    /// controller is worth trying. Anything the entire domain answers identically returns false.
    /// </summary>
    internal static bool ShouldTryNextEndpoint(Exception exception) =>
        exception is LdapException ldapException && !IsDomainWideFailure(ldapException.ErrorCode);

    private static bool IsDomainWideFailure(int errorCode) =>
        errorCode is AuthMethodNotSupported
            or StrongAuthRequired
            or ConfidentialityRequired
            or InappropriateAuthentication
            or InvalidCredentials
            or InsufficientAccessRights;

    /// <summary>
    /// Picks the failure an administrator can act on when every endpoint failed. A directory that
    /// answered and refused something outranks a host that never answered at all, and among equally
    /// ranked failures the first (most preferred) controller wins because that is the one to fix.
    /// </summary>
    internal static Exception SelectMostMeaningfulFailure(IReadOnlyList<Exception> failures)
    {
        ArgumentOutOfRangeException.ThrowIfZero(failures.Count);

        Exception? best = null;
        var bestRank = int.MaxValue;
        foreach (var failure in failures)
        {
            var rank = RankFailure(failure);
            if (rank < bestRank)
            {
                bestRank = rank;
                best = failure;
            }
        }

        return best!;
    }

    private static int RankFailure(Exception exception)
    {
        if (exception is not LdapException ldapException)
        {
            return 1;
        }

        if (IsDomainWideFailure(ldapException.ErrorCode))
        {
            return 0;
        }

        return ldapException.ErrorCode is LdapServerDown
            or LdapTimeout
            or LdapConnectError
            or LdapBusy
            or LdapUnavailable
            ? 3
            : 2;
    }

    /// <summary>
    /// Walks <paramref name="hosts"/> in order and returns the first successfully bound connection.
    /// Connections that fail to bind are disposed before moving on, so no socket is leaked; the
    /// returned connection is owned by the caller.
    ///
    /// Generic over the connection handle because <see cref="LdapConnection.Bind()"/> is not
    /// virtual — the <paramref name="bind"/> delegate is the seam that lets the ordering, fallback,
    /// disposal, and cancellation rules be unit-tested without a live directory.
    /// </summary>
    internal static TConnection BindWithFailover<TConnection>(
        IReadOnlyList<string> hosts,
        Func<string, TConnection> connectionFactory,
        Action<TConnection> bind,
        CancellationToken cancellationToken = default)
        where TConnection : IDisposable
    {
        var failures = new List<Exception>();
        foreach (var host in hosts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var connection = connectionFactory(host);
            try
            {
                bind(connection);
                return connection;
            }
            catch (Exception exception)
            {
                connection.Dispose();
                if (!ShouldTryNextEndpoint(exception))
                {
                    throw;
                }

                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw SelectMostMeaningfulFailure(failures);
        }

        throw new LdapException(
            LdapServerDown,
            "No Active Directory LDAPS endpoint is configured or reachable.");
    }
}
