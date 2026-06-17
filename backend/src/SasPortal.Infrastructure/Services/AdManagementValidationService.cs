using System.DirectoryServices.Protocols;
using System.Net;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed class AdManagementValidationService : IAdManagementValidationService
{
    public Task<AdManagementValidationResult> ValidateConnectionAsync(
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
            || string.IsNullOrWhiteSpace(connection.UsersRootOu)
            || string.IsNullOrWhiteSpace(connection.DisabledUsersOu)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountUserName)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountPassword))
        {
            details.Add(ValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Failed,
                AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings));
            return Task.FromResult(ValidationResult(
                false,
                AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings,
                checkedAt,
                details));
        }

        var bindIdentity = AdServiceAccountBindIdentity.Build(
            connection.ServiceAccountUserName,
            connection.NetbiosDomainName);

        var primaryHost = ResolvePrimaryHost(connection);
        var port = connection.LdapPort;
        var password = connection.ServiceAccountPassword!;

        LdapConnection? primaryConnection = null;
        try
        {
            primaryConnection = TryBind(
                primaryHost,
                port,
                connection.UseSsl,
                bindIdentity,
                password);
        }
        catch
        {
            // Swallow; reflected via null result.
        }

        if (primaryConnection is null)
        {
            details.Add(ValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Failed,
                AdManagementApiMessageKeys.SettingsValidation.ServiceAccountBindFailed));
            return Task.FromResult(ValidationResult(
                false,
                AdManagementApiMessageKeys.SettingsValidation.ServiceAccountBindFailed,
                checkedAt,
                details));
        }

        using (primaryConnection)
        {
            details.Add(new AdManagementValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Ok,
                null));

            if (!TryResolveBase(primaryConnection, connection.BaseDn!))
            {
                details.Add(ValidationDetail(
                    "baseDn",
                    AdManagementValidationStatuses.Failed,
                    AdManagementApiMessageKeys.SettingsValidation.BaseDnNotResolved));
                return Task.FromResult(ValidationResult(
                    false,
                    AdManagementApiMessageKeys.SettingsValidation.BaseDnNotResolved,
                    checkedAt,
                    details));
            }

            details.Add(new AdManagementValidationDetail(
                "baseDn",
                AdManagementValidationStatuses.Ok,
                null));

            if (!TryResolveBase(primaryConnection, connection.DefaultNamingContext!))
            {
                details.Add(ValidationDetail(
                    "defaultNamingContext",
                    AdManagementValidationStatuses.Failed,
                    AdManagementApiMessageKeys.SettingsValidation.DefaultNamingContextNotResolved));
                return Task.FromResult(ValidationResult(
                    false,
                    AdManagementApiMessageKeys.SettingsValidation.DefaultNamingContextNotResolved,
                    checkedAt,
                    details));
            }

            details.Add(new AdManagementValidationDetail(
                "defaultNamingContext",
                AdManagementValidationStatuses.Ok,
                null));

            if (!TryResolveBase(primaryConnection, connection.UsersRootOu!))
            {
                details.Add(ValidationDetail(
                    "usersRootOu",
                    AdManagementValidationStatuses.Failed,
                    AdManagementApiMessageKeys.SettingsValidation.UsersRootOuNotResolved));
                return Task.FromResult(ValidationResult(
                    false,
                    AdManagementApiMessageKeys.SettingsValidation.UsersRootOuNotResolved,
                    checkedAt,
                    details));
            }

            details.Add(new AdManagementValidationDetail(
                "usersRootOu",
                AdManagementValidationStatuses.Ok,
                null));

            if (!TryResolveBase(primaryConnection, connection.DisabledUsersOu!))
            {
                details.Add(ValidationDetail(
                    "disabledUsersOu",
                    AdManagementValidationStatuses.Failed,
                    AdManagementApiMessageKeys.SettingsValidation.DisabledUsersOuNotResolved));
                return Task.FromResult(ValidationResult(
                    false,
                    AdManagementApiMessageKeys.SettingsValidation.DisabledUsersOuNotResolved,
                    checkedAt,
                    details));
            }

            details.Add(new AdManagementValidationDetail(
                "disabledUsersOu",
                AdManagementValidationStatuses.Ok,
                null));

            if (!string.IsNullOrWhiteSpace(connection.GroupsSearchBase))
            {
                if (!TryResolveBase(primaryConnection, connection.GroupsSearchBase!))
                {
                    details.Add(ValidationDetail(
                        "groupsSearchBase",
                        AdManagementValidationStatuses.Failed,
                        AdManagementApiMessageKeys.SettingsValidation.GroupsSearchBaseNotResolved));
                    return Task.FromResult(ValidationResult(
                        false,
                        AdManagementApiMessageKeys.SettingsValidation.GroupsSearchBaseNotResolved,
                        checkedAt,
                        details));
                }

                details.Add(new AdManagementValidationDetail(
                    "groupsSearchBase",
                    AdManagementValidationStatuses.Ok,
                    null));
            }

            if (!string.IsNullOrWhiteSpace(connection.ComputersSearchBase))
            {
                if (!TryResolveBase(primaryConnection, connection.ComputersSearchBase!))
                {
                    details.Add(ValidationDetail(
                        "computersSearchBase",
                        AdManagementValidationStatuses.Failed,
                        AdManagementApiMessageKeys.SettingsValidation.ComputersSearchBaseNotResolved));
                    return Task.FromResult(ValidationResult(
                        false,
                        AdManagementApiMessageKeys.SettingsValidation.ComputersSearchBaseNotResolved,
                        checkedAt,
                        details));
                }

                details.Add(new AdManagementValidationDetail(
                    "computersSearchBase",
                    AdManagementValidationStatuses.Ok,
                    null));
            }
        }

        if (!TryValidateDomainFqdnHost(
                connection.DomainFqdn!,
                port,
                connection.UseSsl,
                bindIdentity,
                password,
                details,
                checkedAt,
                out var domainFqdnFailure))
        {
            return Task.FromResult(domainFqdnFailure!);
        }

        foreach (var dc in connection.PreferredDomainControllers)
        {
            if (string.IsNullOrWhiteSpace(dc))
            {
                continue;
            }

            LdapConnection? dcConnection = null;
            try
            {
                dcConnection = TryBind(
                    dc,
                    port,
                    connection.UseSsl,
                    bindIdentity,
                    password);
            }
            catch
            {
                // Swallow; reflected via null result.
            }

            if (dcConnection is null)
            {
                details.Add(ValidationDetail(
                    $"preferredDomainController:{dc}",
                    AdManagementValidationStatuses.Failed,
                    AdManagementApiMessageKeys.SettingsValidation.PreferredDcUnreachable));
                return Task.FromResult(ValidationResult(
                    false,
                    AdManagementApiMessageKeys.SettingsValidation.PreferredDcUnreachable,
                    checkedAt,
                    details));
            }

            using (dcConnection)
            {
                details.Add(new AdManagementValidationDetail(
                    $"preferredDomainController:{dc}",
                    AdManagementValidationStatuses.Ok,
                    null));
            }
        }

        return Task.FromResult(ValidationResult(
            true,
            AdManagementApiMessageKeys.SettingsValidation.ValidationSucceeded,
            checkedAt,
            details));
    }

    private static AdManagementValidationDetail ValidationDetail(
        string key,
        string status,
        string messageKey) =>
        new(
            key,
            status,
            AdManagementApiMessages.Legacy(messageKey),
            messageKey);

    private static AdManagementValidationResult ValidationResult(
        bool isValid,
        string messageKey,
        DateTimeOffset checkedAt,
        IReadOnlyList<AdManagementValidationDetail> details) =>
        new(
            isValid,
            AdManagementApiMessages.Legacy(messageKey),
            checkedAt,
            details,
            messageKey);

    private static bool TryValidateDomainFqdnHost(
        string domainFqdn,
        int port,
        bool useSsl,
        string bindIdentity,
        string password,
        List<AdManagementValidationDetail> details,
        DateTimeOffset checkedAt,
        out AdManagementValidationResult? failure)
    {
        failure = null;

        LdapConnection? domainConnection = null;
        try
        {
            domainConnection = TryBind(domainFqdn, port, useSsl, bindIdentity, password);
        }
        catch
        {
            // Swallow; reflected via null result.
        }

        if (domainConnection is null)
        {
            details.Add(ValidationDetail(
                "domainFqdn",
                AdManagementValidationStatuses.Failed,
                AdManagementApiMessageKeys.SettingsValidation.DomainFqdnUnreachable));
            failure = ValidationResult(
                false,
                AdManagementApiMessageKeys.SettingsValidation.DomainFqdnUnreachable,
                checkedAt,
                details);
            return false;
        }

        domainConnection.Dispose();
        details.Add(new AdManagementValidationDetail(
            "domainFqdn",
            AdManagementValidationStatuses.Ok,
            null));
        return true;
    }

    private static string ResolvePrimaryHost(AdManagementConnectionParameters connection)
    {
        if (connection.PreferredDomainControllers.Count > 0
            && !string.IsNullOrWhiteSpace(connection.PreferredDomainControllers[0]))
        {
            return connection.PreferredDomainControllers[0];
        }

        return connection.DomainFqdn ?? string.Empty;
    }

    private static LdapConnection? TryBind(
        string host,
        int port,
        bool useSsl,
        string userName,
        string password)
    {
        var identifier = new LdapDirectoryIdentifier(host, port);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Credential = new NetworkCredential(userName, password)
        };

        connection.SessionOptions.ProtocolVersion = 3;
        if (useSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }

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
            var searchRequest = new SearchRequest(
                distinguishedName,
                "(objectClass=*)",
                SearchScope.Base,
                "distinguishedName")
            {
                SizeLimit = 1
            };

            var response = (SearchResponse)connection.SendRequest(searchRequest);
            return response.ResultCode == ResultCode.Success && response.Entries.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
