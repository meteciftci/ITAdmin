using System.Collections;
using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService(
    IAdManagementSettingsService settingsService,
    IAdAttributeMappingService attributeMappingService,
    IAdOperationLogService adOperationLogService,
    IAuditLogWriter auditLogWriter,
    IAdManagementNotificationEnqueueService notificationEnqueueService,
    ILogger<AdUserDirectoryService> logger) : IAdUserDirectoryService
{
    private const string AdManagementDisabledMessage = "AD yönetim modülü etkin değil.";
    private const string AdManagementNotConfiguredMessage =
        "AD yönetim ayarları yapılandırılmamış. Lütfen önce bağlantı ayarlarını kaydedin.";
    private const string MissingServiceAccountPasswordMessage =
        "AD yönetim servis hesabı parolası tanımlı değil.";
    private const string DirectoryQueryFailedMessage = "AD kullanıcıları okunamadı.";
    private const string UserNotFoundMessage = "AD kullanıcısı bulunamadı.";
    private static readonly TimeSpan LdapOperationTimeout = TimeSpan.FromSeconds(30);

    public async Task<AdUserDirectorySearchResult> SearchUsersAsync(
        AdUserSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageSize = AdLdapValueConverter.ClampPageSize(query.PageSize);
        var pageNumber = AdLdapValueConverter.NormalizePageNumber(query.PageNumber);

        if (!AdLdapAttributeCatalog.IsSearchTermValid(query.Search))
        {
            return new AdUserDirectorySearchResult(
                true,
                string.Empty,
                new AdUserSearchPage([], pageNumber, pageSize, false));
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdUserDirectorySearchResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind);
        }

        var mappings = await attributeMappingService.GetMappingsAsync(cancellationToken);
        var context = connectionResult.Context;
        var searchBase = ResolveListSearchBase(context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdUserDirectorySearchResult(
                false,
                AdManagementNotConfiguredMessage,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            var searchableMappings = AdLdapAttributeCatalog.GetSearchableMappingAttributeNames(mappings);
            var filter = AdLdapAttributeCatalog.BuildUserSearchFilter(
                query.Search!.Trim(),
                query.Status,
                searchableMappings);
            var listAttributes = AdLdapAttributeCatalog.BuildListLdapAttributeNames(mappings);
            var items = new List<AdUserListItem>(pageSize);
            byte[]? cookie = null;
            var hasNextPage = false;

            for (var currentPage = 1; currentPage <= pageNumber; currentPage++)
            {
                var searchRequest = new SearchRequest(
                    searchBase,
                    filter,
                    SearchScope.Subtree,
                    listAttributes)
                {
                    TimeLimit = LdapOperationTimeout,
                };

                var pageControl = new PageResultRequestControl(pageSize)
                {
                    Cookie = cookie,
                };
                searchRequest.Controls.Add(pageControl);

                var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
                if (response.ResultCode != ResultCode.Success)
                {
                    return ConnectionFailed();
                }

                var pageResponse = response.Controls
                    .OfType<PageResultResponseControl>()
                    .FirstOrDefault();
                cookie = pageResponse?.Cookie;
                hasNextPage = cookie is { Length: > 0 };

                if (currentPage != pageNumber)
                {
                    continue;
                }

                foreach (SearchResultEntry entry in response.Entries)
                {
                    if (TryMapListItem(entry, out var item))
                    {
                        items.Add(item);
                    }
                }
            }

            return new AdUserDirectorySearchResult(
                true,
                string.Empty,
                new AdUserSearchPage(items, pageNumber, pageSize, hasNextPage));
        }
        catch (LdapException)
        {
            return ConnectionFailed();
        }
        catch (Exception)
        {
            return ConnectionFailed();
        }
    }

    public async Task<AdUserDirectoryDetailResult> GetUserByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdUserDirectoryDetailResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind);
        }

        var mappings = await attributeMappingService.GetMappingsAsync(cancellationToken);
        var activeMappings = mappings.Where(static mapping => mapping.IsEnabled).ToList();
        var context = connectionResult.Context;
        var searchBase = ResolveDetailSearchBase(context.Connection);
        if (string.IsNullOrWhiteSpace(searchBase))
        {
            return new AdUserDirectoryDetailResult(
                false,
                AdManagementNotConfiguredMessage,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(id);
            var filter =
                $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))(objectGUID={guidFilter}))";

            var detailAttributes = AdLdapAttributeCatalog.BuildDetailLdapAttributeNames(activeMappings);
            var searchRequest = new SearchRequest(
                searchBase,
                filter,
                SearchScope.Subtree,
                detailAttributes)
            {
                SizeLimit = 2,
                TimeLimit = LdapOperationTimeout,
            };

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return ConnectionFailedDetail();
            }

            if (response.Entries.Count == 0)
            {
                return new AdUserDirectoryDetailResult(
                    false,
                    UserNotFoundMessage,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            if (!TryMapDetailItem(response.Entries[0], activeMappings, out var detail))
            {
                return new AdUserDirectoryDetailResult(
                    false,
                    UserNotFoundMessage,
                    null,
                    AdDirectoryFailureKind.NotFound);
            }

            detail = TryEnrichDetailWithResolvedManager(ldapConnection, detail);

            return new AdUserDirectoryDetailResult(true, string.Empty, detail);
        }
        catch (LdapException)
        {
            return ConnectionFailedDetail();
        }
        catch (Exception)
        {
            return ConnectionFailedDetail();
        }
    }

    private async Task<ConnectionResolveResult> ResolveConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        if (!settings.IsEnabled)
        {
            return ConnectionResolveResult.Failed(
                AdManagementDisabledMessage,
                AdDirectoryFailureKind.Disabled);
        }

        var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);
        if (connection is null
            || string.IsNullOrWhiteSpace(connection.ServiceAccountUserName)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountPassword))
        {
            return ConnectionResolveResult.Failed(
                string.IsNullOrWhiteSpace(connection?.ServiceAccountPassword)
                    ? MissingServiceAccountPasswordMessage
                    : AdManagementNotConfiguredMessage,
                string.IsNullOrWhiteSpace(connection?.ServiceAccountPassword)
                    ? AdDirectoryFailureKind.MissingPassword
                    : AdDirectoryFailureKind.NotConfigured);
        }

        var ldapsError = AdDirectoryConnectionRequirements.GetLdapsRequiredErrorMessage(connection.UseSsl);
        if (ldapsError is not null)
        {
            return ConnectionResolveResult.Failed(ldapsError, AdDirectoryFailureKind.InvalidRequest);
        }

        return ConnectionResolveResult.Success(new DirectoryConnectionContext(connection));
    }

    private static LdapConnection CreateBoundConnection(DirectoryConnectionContext context)
    {
        var host = ResolvePrimaryHost(context.Connection);
        var bindIdentity = AdServiceAccountBindIdentity.Build(
            context.Connection.ServiceAccountUserName,
            context.Connection.NetbiosDomainName);

        var identifier = new LdapDirectoryIdentifier(host, context.Connection.LdapPort);
        var ldapConnection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Credential = new NetworkCredential(bindIdentity, context.Connection.ServiceAccountPassword),
        };

        ldapConnection.SessionOptions.ProtocolVersion = 3;
        ldapConnection.Timeout = LdapOperationTimeout;
        if (context.Connection.UseSsl)
        {
            ldapConnection.SessionOptions.SecureSocketLayer = true;
        }

        ldapConnection.Bind();
        return ldapConnection;
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

    private static string? ResolveListSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.UsersRootOu)
            ? connection.DefaultNamingContext ?? connection.BaseDn
            : connection.UsersRootOu;

    private static string? ResolveDetailSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.DefaultNamingContext)
            ? connection.BaseDn
            : connection.DefaultNamingContext;

    private static bool TryMapListItem(SearchResultEntry entry, out AdUserListItem item)
    {
        item = null!;
        if (!TryGetObjectGuid(entry, out var objectGuid))
        {
            return false;
        }

        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var userAccountControl = GetFirstInt(entry, "userAccountControl");
        var lockoutTime = GetFirstLong(entry, "lockoutTime");

        item = new AdUserListItem(
            objectGuid.ToString("D"),
            distinguishedName,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "userPrincipalName"),
            GetFirstString(entry, "displayName"),
            GetFirstString(entry, "mail"),
            GetFirstString(entry, "department"),
            AdLdapValueConverter.IsAccountEnabled(userAccountControl),
            AdLdapValueConverter.IsAccountLockedOut(lockoutTime),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenCreated")),
            AdLdapValueConverter.ParseGeneralizedTime(GetFirstString(entry, "whenChanged")),
            AdLdapValueConverter.FromAdFileTime(GetFirstLong(entry, "lastLogonTimestamp")));

        return true;
    }

    private static bool TryMapDetailItem(
        SearchResultEntry entry,
        IReadOnlyList<AdAttributeMappingItem> activeMappings,
        out AdUserDetail detail)
    {
        detail = null!;
        if (!TryMapListItem(entry, out var listItem))
        {
            return false;
        }

        var groups = AdLdapDnHelper.BuildGroupMemberships(GetAllStrings(entry, "memberOf"));

        var mappedAttributes = BuildMappedAttributes(entry, activeMappings);

        var userAccountControl = GetFirstInt(entry, "userAccountControl");
        var lockoutTime = GetFirstLong(entry, "lockoutTime");
        var lastLogonTimestampAt = AdLdapValueConverter.FromAdFileTime(
            GetFirstLong(entry, "lastLogonTimestamp"));

        var managerDistinguishedName = GetFirstString(entry, "manager");

        detail = new AdUserDetail(
            listItem.Id,
            listItem.DistinguishedName,
            listItem.SamAccountName,
            listItem.UserPrincipalName,
            listItem.DisplayName,
            listItem.Mail,
            GetFirstString(entry, "givenName"),
            GetFirstString(entry, "sn"),
            listItem.Department,
            listItem.IsEnabled,
            listItem.IsLockedOut,
            AdLdapValueConverter.FromAdFileTime(GetFirstLong(entry, "pwdLastSet")),
            listItem.LastLogonAt,
            listItem.WhenCreated,
            listItem.WhenChanged,
            userAccountControl,
            AdLdapValueConverter.FromAdFileTime(GetFirstLong(entry, "accountExpires")),
            AdLdapValueConverter.FromAdFileTime(lockoutTime),
            GetFirstInt(entry, "badPwdCount"),
            AdLdapValueConverter.FromAdFileTime(GetFirstLong(entry, "badPasswordTime")),
            lastLogonTimestampAt,
            groups,
            mappedAttributes,
            managerDistinguishedName);

        return true;
    }

    private AdUserDetail TryEnrichDetailWithResolvedManager(LdapConnection ldapConnection, AdUserDetail detail)
    {
        if (string.IsNullOrWhiteSpace(detail.ManagerDistinguishedName))
        {
            return detail;
        }

        try
        {
            if (!TryResolveManagerByDistinguishedName(
                    ldapConnection,
                    detail.ManagerDistinguishedName,
                    out var managerId,
                    out var managerSamAccountName,
                    out var managerUserPrincipalName,
                    out var managerDisplayName))
            {
                return detail;
            }

            return detail with
            {
                ManagerId = managerId,
                ManagerSamAccountName = managerSamAccountName,
                ManagerUserPrincipalName = managerUserPrincipalName,
                ManagerDisplayName = managerDisplayName,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AD user manager resolve failed for user {UserId}",
                detail.Id);
            return detail;
        }
    }

    private static bool TryResolveManagerByDistinguishedName(
        LdapConnection ldapConnection,
        string managerDistinguishedName,
        out string? managerId,
        out string? managerSamAccountName,
        out string? managerUserPrincipalName,
        out string? managerDisplayName)
    {
        managerId = null;
        managerSamAccountName = null;
        managerUserPrincipalName = null;
        managerDisplayName = null;

        var searchRequest = new SearchRequest(
            managerDistinguishedName.Trim(),
            "(&(objectCategory=person)(objectClass=user))",
            SearchScope.Base,
            "objectGUID",
            "sAMAccountName",
            "userPrincipalName",
            "displayName")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        var entry = response.Entries[0];
        if (!TryGetObjectGuid(entry, out var objectGuid))
        {
            return false;
        }

        managerId = objectGuid.ToString("D");
        managerSamAccountName = GetFirstString(entry, "sAMAccountName");
        managerUserPrincipalName = GetFirstString(entry, "userPrincipalName");
        managerDisplayName = GetFirstString(entry, "displayName");
        return true;
    }

    private static IReadOnlyList<MappedAdUserAttribute> BuildMappedAttributes(
        SearchResultEntry entry,
        IReadOnlyList<AdAttributeMappingItem> activeMappings) =>
        AdUserMappedAttributeBuilder.Build(
            attributeName => GetAllStrings(entry, attributeName),
            activeMappings);

    private static bool TryGetObjectGuid(SearchResultEntry entry, out Guid objectGuid)
    {
        objectGuid = Guid.Empty;
        var guidBytes = GetFirstBytes(entry, "objectGUID");
        if (guidBytes is null || guidBytes.Length != 16)
        {
            return false;
        }

        try
        {
            objectGuid = new Guid(guidBytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetFirstString(SearchResultEntry entry, string attributeName)
    {
        var attribute = TryGetAttribute(entry, attributeName);
        if (attribute is null || attribute.Count == 0)
        {
            return null;
        }

        return NormalizeOptional(GetRawValueAsString(attribute[0]));
    }

    private static IReadOnlyList<string> GetAllStrings(SearchResultEntry entry, string attributeName)
    {
        var attribute = TryGetAttribute(entry, attributeName);
        if (attribute is null)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var raw in attribute)
        {
            var text = NormalizeOptional(GetRawValueAsString(raw));
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text);
            }
        }

        return values;
    }

    private static int? GetFirstInt(SearchResultEntry entry, string attributeName)
    {
        var text = GetFirstString(entry, attributeName);
        return int.TryParse(text, out var value) ? value : null;
    }

    private static long? GetFirstLong(SearchResultEntry entry, string attributeName)
    {
        var text = GetFirstString(entry, attributeName);
        return long.TryParse(text, out var value) ? value : null;
    }

    private static byte[]? GetFirstBytes(SearchResultEntry entry, string attributeName)
    {
        var attribute = TryGetAttribute(entry, attributeName);
        if (attribute is null || attribute.Count == 0)
        {
            return null;
        }

        return attribute[0] as byte[];
    }

    private static DirectoryAttribute? TryGetAttribute(SearchResultEntry entry, string attributeName)
    {
        foreach (DictionaryEntry kv in entry.Attributes)
        {
            if (string.Equals(kv.Key.ToString(), attributeName, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value as DirectoryAttribute;
            }
        }

        return null;
    }

    private static string? GetRawValueAsString(object raw) =>
        raw switch
        {
            string text => text,
            byte[] bytes => DecodeLdapString(bytes),
            _ => raw.ToString(),
        };

    private static string? DecodeLdapString(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        try
        {
            return System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdUserDirectorySearchResult ConnectionFailed() =>
        new(false, DirectoryQueryFailedMessage, null, AdDirectoryFailureKind.ConnectionFailed);

    private static AdUserDirectoryDetailResult ConnectionFailedDetail() =>
        new(false, DirectoryQueryFailedMessage, null, AdDirectoryFailureKind.ConnectionFailed);

    private sealed class DirectoryConnectionContext(AdManagementConnectionParameters connection)
    {
        public AdManagementConnectionParameters Connection { get; } = connection;
    }

    private sealed class ConnectionResolveResult
    {
        public bool IsSuccess { get; init; }
        public string Message { get; init; } = string.Empty;
        public DirectoryConnectionContext? Context { get; init; }
        public AdDirectoryFailureKind? FailureKind { get; init; }

        public static ConnectionResolveResult Success(DirectoryConnectionContext context) =>
            new() { IsSuccess = true, Context = context };

        public static ConnectionResolveResult Failed(string message, AdDirectoryFailureKind kind) =>
            new() { IsSuccess = false, Message = message, FailureKind = kind };
    }
}
