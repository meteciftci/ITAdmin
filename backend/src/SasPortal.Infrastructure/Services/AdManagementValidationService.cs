using System.DirectoryServices.Protocols;
using System.Net;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed class AdManagementValidationService(
    IAdManagementSettingsService settingsService) : IAdManagementValidationService
{
    private const string MissingRequiredSettings = "AD yönetim ayarları eksik. Lütfen önce gerekli alanları kaydedin.";
    private const string ServiceAccountBindFailed = "AD yönetim servis hesabı ile bağlantı kurulamadı.";
    private const string BaseDnNotResolved = "Base DN çözümlenemedi.";
    private const string UsersRootOuNotResolved = "Users root OU çözümlenemedi.";
    private const string DisabledUsersOuNotResolved = "Pasif kullanıcılar OU çözümlenemedi.";
    private const string GroupsSearchBaseNotResolved = "Gruplar arama base çözümlenemedi.";
    private const string ComputersSearchBaseNotResolved = "Bilgisayarlar arama base çözümlenemedi.";
    private const string PreferredDcUnreachable = "Tercih edilen DC erişilemedi.";
    private const string ValidationSucceeded = "AD yönetim ayarları doğrulandı.";

    public async Task<AdManagementValidationResult> ValidateAsync(
        AdManagementValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;

        var checkedAt = DateTimeOffset.UtcNow;
        var details = new List<AdManagementValidationDetail>();

        var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);

        if (connection is null
            || string.IsNullOrWhiteSpace(connection.DomainFqdn)
            || string.IsNullOrWhiteSpace(connection.BaseDn)
            || string.IsNullOrWhiteSpace(connection.UsersRootOu)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountUserName)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountPassword))
        {
            details.Add(new AdManagementValidationDetail(
                "serviceAccountBind",
                AdManagementValidationStatuses.Failed,
                MissingRequiredSettings));
            return new AdManagementValidationResult(false, MissingRequiredSettings, checkedAt, details);
        }

        var primaryHost = ResolvePrimaryHost(connection);
        var port = connection.LdapPort;

        LdapConnection? primaryConnection = null;
        try
        {
            primaryConnection = TryBind(
                primaryHost,
                port,
                connection.UseSsl,
                connection.ServiceAccountUserName!,
                connection.ServiceAccountPassword!);
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
            return new AdManagementValidationResult(false, ServiceAccountBindFailed, checkedAt, details);
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
                return new AdManagementValidationResult(false, BaseDnNotResolved, checkedAt, details);
            }

            details.Add(new AdManagementValidationDetail(
                "baseDn",
                AdManagementValidationStatuses.Ok,
                null));

            if (!TryResolveBase(primaryConnection, connection.UsersRootOu!))
            {
                details.Add(new AdManagementValidationDetail(
                    "usersRootOu",
                    AdManagementValidationStatuses.Failed,
                    UsersRootOuNotResolved));
                return new AdManagementValidationResult(false, UsersRootOuNotResolved, checkedAt, details);
            }

            details.Add(new AdManagementValidationDetail(
                "usersRootOu",
                AdManagementValidationStatuses.Ok,
                null));

            if (!string.IsNullOrWhiteSpace(connection.DisabledUsersOu))
            {
                if (!TryResolveBase(primaryConnection, connection.DisabledUsersOu!))
                {
                    details.Add(new AdManagementValidationDetail(
                        "disabledUsersOu",
                        AdManagementValidationStatuses.Failed,
                        DisabledUsersOuNotResolved));
                    return new AdManagementValidationResult(false, DisabledUsersOuNotResolved, checkedAt, details);
                }

                details.Add(new AdManagementValidationDetail(
                    "disabledUsersOu",
                    AdManagementValidationStatuses.Ok,
                    null));
            }

            if (!string.IsNullOrWhiteSpace(connection.GroupsSearchBase))
            {
                if (!TryResolveBase(primaryConnection, connection.GroupsSearchBase!))
                {
                    details.Add(new AdManagementValidationDetail(
                        "groupsSearchBase",
                        AdManagementValidationStatuses.Failed,
                        GroupsSearchBaseNotResolved));
                    return new AdManagementValidationResult(false, GroupsSearchBaseNotResolved, checkedAt, details);
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
                    return new AdManagementValidationResult(false, ComputersSearchBaseNotResolved, checkedAt, details);
                }

                details.Add(new AdManagementValidationDetail(
                    "computersSearchBase",
                    AdManagementValidationStatuses.Ok,
                    null));
            }
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
                    connection.ServiceAccountUserName!,
                    connection.ServiceAccountPassword!);
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
                return new AdManagementValidationResult(false, PreferredDcUnreachable, checkedAt, details);
            }

            using (dcConnection)
            {
                details.Add(new AdManagementValidationDetail(
                    $"preferredDomainController:{dc}",
                    AdManagementValidationStatuses.Ok,
                    null));
            }
        }

        return new AdManagementValidationResult(true, ValidationSucceeded, checkedAt, details);
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
