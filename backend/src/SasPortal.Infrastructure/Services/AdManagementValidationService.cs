using System.DirectoryServices.Protocols;
using System.Net;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed class AdManagementValidationService : IAdManagementValidationService
{
    private const string MissingRequiredSettings =
        "AD yönetim ayarları için zorunlu alanlar eksik.";
    private const string ServiceAccountBindFailed =
        "AD yönetim servis hesabı ile bağlantı kurulamadı. NetBIOS domain adı, servis hesabı kullanıcı adı veya parola hatalı olabilir.";
    private const string DomainFqdnUnreachable =
        "Domain FQDN erişilemedi veya doğrulanamadı.";
    private const string BaseDnNotResolved = "Base DN çözümlenemedi.";
    private const string DefaultNamingContextNotResolved = "Default naming context çözümlenemedi.";
    private const string UsersRootOuNotResolved = "Users root OU çözümlenemedi.";
    private const string DisabledUsersOuNotResolved = "Pasif kullanıcılar OU çözümlenemedi.";
    private const string GroupsSearchBaseNotResolved = "Gruplar arama base çözümlenemedi.";
    private const string ComputersSearchBaseNotResolved = "Bilgisayarlar arama base çözümlenemedi.";
    private const string PreferredDcUnreachable = "Tercih edilen DC erişilemedi.";
    private const string ValidationSucceeded = "AD yönetim ayarları doğrulandı.";

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
            details.Add(new AdManagementValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Failed,
                MissingRequiredSettings));
            return Task.FromResult(new AdManagementValidationResult(
                false,
                MissingRequiredSettings,
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
            details.Add(new AdManagementValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Failed,
                ServiceAccountBindFailed));
            return Task.FromResult(new AdManagementValidationResult(
                false,
                ServiceAccountBindFailed,
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
                details.Add(new AdManagementValidationDetail(
                    "baseDn",
                    AdManagementValidationStatuses.Failed,
                    BaseDnNotResolved));
                return Task.FromResult(new AdManagementValidationResult(
                    false,
                    BaseDnNotResolved,
                    checkedAt,
                    details));
            }

            details.Add(new AdManagementValidationDetail(
                "baseDn",
                AdManagementValidationStatuses.Ok,
                null));

            if (!TryResolveBase(primaryConnection, connection.DefaultNamingContext!))
            {
                details.Add(new AdManagementValidationDetail(
                    "defaultNamingContext",
                    AdManagementValidationStatuses.Failed,
                    DefaultNamingContextNotResolved));
                return Task.FromResult(new AdManagementValidationResult(
                    false,
                    DefaultNamingContextNotResolved,
                    checkedAt,
                    details));
            }

            details.Add(new AdManagementValidationDetail(
                "defaultNamingContext",
                AdManagementValidationStatuses.Ok,
                null));

            if (!TryResolveBase(primaryConnection, connection.UsersRootOu!))
            {
                details.Add(new AdManagementValidationDetail(
                    "usersRootOu",
                    AdManagementValidationStatuses.Failed,
                    UsersRootOuNotResolved));
                return Task.FromResult(new AdManagementValidationResult(
                    false,
                    UsersRootOuNotResolved,
                    checkedAt,
                    details));
            }

            details.Add(new AdManagementValidationDetail(
                "usersRootOu",
                AdManagementValidationStatuses.Ok,
                null));

            if (!TryResolveBase(primaryConnection, connection.DisabledUsersOu!))
            {
                details.Add(new AdManagementValidationDetail(
                    "disabledUsersOu",
                    AdManagementValidationStatuses.Failed,
                    DisabledUsersOuNotResolved));
                return Task.FromResult(new AdManagementValidationResult(
                    false,
                    DisabledUsersOuNotResolved,
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
                    details.Add(new AdManagementValidationDetail(
                        "groupsSearchBase",
                        AdManagementValidationStatuses.Failed,
                        GroupsSearchBaseNotResolved));
                    return Task.FromResult(new AdManagementValidationResult(
                        false,
                        GroupsSearchBaseNotResolved,
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
                    details.Add(new AdManagementValidationDetail(
                        "computersSearchBase",
                        AdManagementValidationStatuses.Failed,
                        ComputersSearchBaseNotResolved));
                    return Task.FromResult(new AdManagementValidationResult(
                        false,
                        ComputersSearchBaseNotResolved,
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
                details.Add(new AdManagementValidationDetail(
                    $"preferredDomainController:{dc}",
                    AdManagementValidationStatuses.Failed,
                    PreferredDcUnreachable));
                return Task.FromResult(new AdManagementValidationResult(
                    false,
                    PreferredDcUnreachable,
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

        return Task.FromResult(new AdManagementValidationResult(
            true,
            ValidationSucceeded,
            checkedAt,
            details));
    }

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
            details.Add(new AdManagementValidationDetail(
                "domainFqdn",
                AdManagementValidationStatuses.Failed,
                DomainFqdnUnreachable));
            failure = new AdManagementValidationResult(
                false,
                DomainFqdnUnreachable,
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
