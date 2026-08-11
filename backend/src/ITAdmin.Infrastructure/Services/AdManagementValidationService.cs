using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Sockets;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

public sealed class AdManagementValidationService(ILdapService ldapService) : IAdManagementValidationService
{
    public async Task<AdManagementValidationResult> ValidateConnectionAsync(
        AdManagementConnectionParameters connection,
        AdManagementValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        cancellationToken.ThrowIfCancellationRequested();

        var checkedAt = DateTimeOffset.UtcNow;
        var details = new List<AdManagementValidationDetail>();

        if (string.IsNullOrWhiteSpace(connection.DomainFqdn)
            || string.IsNullOrWhiteSpace(connection.NetbiosDomainName)
            || string.IsNullOrWhiteSpace(connection.DefaultNamingContext)
            || string.IsNullOrWhiteSpace(connection.BaseDn)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountUserName)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountPassword))
        {
            details.Add(ValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Failed,
                AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings));
            return ValidationResult(false, AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings, checkedAt, details);
        }

        // Same ordered-failover host list the runtime directory operations use, so what an
        // administrator validates here is exactly what production will attempt.
        var hosts = AdDirectoryFailoverPolicy.ResolveOrderedHosts(connection);
        var hasPreferredControllers = connection.PreferredDomainControllers.Any(host => !string.IsNullOrWhiteSpace(host));

        // Domain-FQDN resolution is reported, not gated: when explicit preferred controllers are
        // configured they can still serve every operation, and short-circuiting here would hide the
        // per-endpoint DNS/TCP/TLS/certificate/bind stages that tell the administrator what to fix.
        var domainResolves = await DomainResolvesAsync(connection.DomainFqdn!, cancellationToken);
        details.Add(ValidationDetail(
            "domainFqdn",
            domainResolves
                ? AdManagementValidationStatuses.Ok
                : hasPreferredControllers
                    ? AdManagementValidationStatuses.Warning
                    : AdManagementValidationStatuses.Failed,
            domainResolves
                ? LdapConnectionDiagnosticMessageKeys.DnsResolved
                : AdManagementApiMessageKeys.SettingsValidation.DomainFqdnUnreachable,
            connection.DomainFqdn));

        if (!domainResolves && !hasPreferredControllers)
        {
            return ValidationResult(
                false,
                AdManagementApiMessageKeys.SettingsValidation.DomainFqdnUnreachable,
                checkedAt,
                details);
        }
        var probeResults = new List<LdapConnectionDiagnosticResult>();
        LdapConnectionDiagnosticResult? selectedProbe = null;
        foreach (var host in hosts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ldapService.DiagnoseConnectionAsync(
                new LdapConnectionDiagnosticRequest(
                    host,
                    AdServiceAccountBindIdentity.Build(
                        connection.ServiceAccountUserName,
                        connection.NetbiosDomainName),
                    null,
                    connection.ServiceAccountPassword!),
                cancellationToken);
            probeResults.Add(result);

            if (result.IsValid && selectedProbe is null)
            {
                selectedProbe = result;
            }

            if (result.Details.Any(detail =>
                    detail.MessageKey == LdapConnectionDiagnosticMessageKeys.BindCredentialsRejected))
            {
                break;
            }
        }

        AppendEndpointDetails(details, probeResults, selectedProbe is not null);
        if (selectedProbe is null)
        {
            details.Add(ValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Failed,
                AdManagementApiMessageKeys.SettingsValidation.ServiceAccountBindFailed));
            return ValidationResult(false, AdManagementApiMessageKeys.SettingsValidation.ServiceAccountBindFailed, checkedAt, details);
        }

        var bindIdentity = AdServiceAccountBindIdentity.Build(
            connection.ServiceAccountUserName,
            connection.NetbiosDomainName);
        using var selectedConnection = TryBind(
            selectedProbe.Host,
            bindIdentity,
            connection.ServiceAccountPassword!);
        if (selectedConnection is null)
        {
            details.Add(ValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Failed,
                AdManagementApiMessageKeys.SettingsValidation.ServiceAccountBindFailed));
            return ValidationResult(false, AdManagementApiMessageKeys.SettingsValidation.ServiceAccountBindFailed, checkedAt, details);
        }

        details.Add(ValidationDetail(
            "serviceAccountBind",
            AdManagementValidationStatuses.Ok,
            LdapConnectionDiagnosticMessageKeys.BindSucceeded,
            selectedProbe.Host));

        if (!ValidateBase(selectedConnection, connection.BaseDn!, "baseDn", AdManagementApiMessageKeys.SettingsValidation.BaseDnNotResolved, details))
        {
            return ValidationResult(false, AdManagementApiMessageKeys.SettingsValidation.BaseDnNotResolved, checkedAt, details);
        }

        if (!ValidateBase(selectedConnection, connection.DefaultNamingContext!, "defaultNamingContext", AdManagementApiMessageKeys.SettingsValidation.DefaultNamingContextNotResolved, details))
        {
            return ValidationResult(false, AdManagementApiMessageKeys.SettingsValidation.DefaultNamingContextNotResolved, checkedAt, details);
        }

        var optionalBases = new[]
        {
            (connection.UsersRootOu, "usersRootOu", AdManagementApiMessageKeys.SettingsValidation.UsersRootOuNotResolved),
            (connection.DisabledUsersOu, "disabledUsersOu", AdManagementApiMessageKeys.SettingsValidation.DisabledUsersOuNotResolved),
            (connection.GroupsSearchBase, "groupsSearchBase", AdManagementApiMessageKeys.SettingsValidation.GroupsSearchBaseNotResolved),
            (connection.ComputersSearchBase, "computersSearchBase", AdManagementApiMessageKeys.SettingsValidation.ComputersSearchBaseNotResolved),
        };
        foreach (var (distinguishedName, key, failureKey) in optionalBases)
        {
            if (!string.IsNullOrWhiteSpace(distinguishedName)
                && !ValidateBase(selectedConnection, distinguishedName, key, failureKey, details))
            {
                return ValidationResult(false, failureKey, checkedAt, details);
            }
        }

        return ValidationResult(
            true,
            AdManagementApiMessageKeys.SettingsValidation.ValidationSucceeded,
            checkedAt,
            details);
    }

    private static async Task<bool> DomainResolvesAsync(string domainFqdn, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(domainFqdn, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
            return addresses.Length > 0;
        }
        catch (Exception exception) when (exception is SocketException or TimeoutException or OperationCanceledException)
        {
            return false;
        }
    }

    private static void AppendEndpointDetails(
        List<AdManagementValidationDetail> target,
        IReadOnlyList<LdapConnectionDiagnosticResult> probeResults,
        bool hasWorkingEndpoint)
    {
        for (var index = 0; index < probeResults.Count; index++)
        {
            var probe = probeResults[index];
            foreach (var detail in probe.Details)
            {
                var status = detail.Status;
                if (hasWorkingEndpoint && !probe.IsValid
                    && status == LdapConnectionDiagnosticStatuses.Failed)
                {
                    status = LdapConnectionDiagnosticStatuses.Warning;
                }

                target.Add(new AdManagementValidationDetail(
                    $"endpoint:{index}:{detail.Key}",
                    status,
                    detail.MessageKey,
                    detail.MessageParams));
            }
        }
    }

    private static bool ValidateBase(
        LdapConnection connection,
        string distinguishedName,
        string key,
        string failureMessageKey,
        List<AdManagementValidationDetail> details)
    {
        if (!TryResolveBase(connection, distinguishedName))
        {
            details.Add(ValidationDetail(key, AdManagementValidationStatuses.Failed, failureMessageKey));
            return false;
        }

        details.Add(new AdManagementValidationDetail(
            key,
            AdManagementValidationStatuses.Ok,
            key == "baseDn"
                ? LdapConnectionDiagnosticMessageKeys.BaseDnResolved
                : LdapConnectionDiagnosticMessageKeys.DirectoryContextResolved));
        return true;
    }

    private static AdManagementValidationDetail ValidationDetail(
        string key,
        string status,
        string messageKey,
        string? host = null) =>
        new(
            key,
            status,
            messageKey,
            string.IsNullOrWhiteSpace(host)
                ? null
                : new Dictionary<string, object> { ["host"] = host });

    private static AdManagementValidationResult ValidationResult(
        bool isValid,
        string messageKey,
        DateTimeOffset checkedAt,
        IReadOnlyList<AdManagementValidationDetail> details) =>
        new(isValid, messageKey, checkedAt, details);

    private static LdapConnection? TryBind(string host, string userName, string password)
    {
        var identifier = new LdapDirectoryIdentifier(host, LdapConnectionDefaults.StandardLdapsPort);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Credential = new NetworkCredential(userName, password),
            Timeout = TimeSpan.FromSeconds(10),
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = true;

        try
        {
            connection.Bind();
            return connection;
        }
        catch
        {
            connection.Dispose();
            return null;
        }
    }

    private static bool TryResolveBase(LdapConnection connection, string distinguishedName)
    {
        try
        {
            var response = (SearchResponse)connection.SendRequest(new SearchRequest(
                distinguishedName,
                "(objectClass=*)",
                SearchScope.Base,
                "distinguishedName") { SizeLimit = 1 });
            return response.ResultCode == ResultCode.Success && response.Entries.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
