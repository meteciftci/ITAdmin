using System.Collections;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

/// <summary>
/// Shared plumbing for the Active Directory directory services: LDAP connection
/// resolution/binding, <see cref="SearchResultEntry"/> attribute parsing helpers,
/// logging, and the common constructor dependencies. Focused per-domain services
/// (users, groups, computers, deleted objects, organizational units) derive from
/// this base so the low-level directory helpers are shared without duplication.
/// </summary>
public abstract class AdDirectoryServiceBase
{
    private protected static readonly TimeSpan LdapOperationTimeout = TimeSpan.FromSeconds(30);

    private protected readonly IAdManagementSettingsService settingsService;
    private protected readonly IAdAttributeMappingService attributeMappingService;
    private protected readonly IAdOperationLogService adOperationLogService;
    private protected readonly IAuditLogWriter auditLogWriter;
    private protected readonly IAdManagementNotificationEnqueueService notificationEnqueueService;
    private protected readonly IAdDeletedObjectRestoreCommandRunner deletedObjectRestoreCommandRunner;
    private protected readonly ILogger logger;

    private protected AdDirectoryServiceBase(
        IAdManagementSettingsService settingsService,
        IAdAttributeMappingService attributeMappingService,
        IAdOperationLogService adOperationLogService,
        IAuditLogWriter auditLogWriter,
        IAdManagementNotificationEnqueueService notificationEnqueueService,
        IAdDeletedObjectRestoreCommandRunner deletedObjectRestoreCommandRunner,
        ILogger logger)
    {
        this.settingsService = settingsService;
        this.attributeMappingService = attributeMappingService;
        this.adOperationLogService = adOperationLogService;
        this.auditLogWriter = auditLogWriter;
        this.notificationEnqueueService = notificationEnqueueService;
        this.deletedObjectRestoreCommandRunner = deletedObjectRestoreCommandRunner;
        this.logger = logger;
    }

    private protected async Task<ConnectionResolveResult> ResolveConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        if (!settings.IsEnabled)
        {
            return ConnectionResolveResult.Failed(
                AdManagementApiMessageKeys.Common.ModuleDisabled,
                AdDirectoryFailureKind.Disabled);
        }

        var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);
        if (connection is null
            || string.IsNullOrWhiteSpace(connection.ServiceAccountUserName)
            || string.IsNullOrWhiteSpace(connection.ServiceAccountPassword))
        {
            var messageKey = string.IsNullOrWhiteSpace(connection?.ServiceAccountPassword)
                ? AdManagementApiMessageKeys.Common.MissingServiceAccountPassword
                : AdManagementApiMessageKeys.Common.NotConfigured;
            return ConnectionResolveResult.Failed(
                messageKey,
                string.IsNullOrWhiteSpace(connection?.ServiceAccountPassword)
                    ? AdDirectoryFailureKind.MissingPassword
                    : AdDirectoryFailureKind.NotConfigured);
        }

        return ConnectionResolveResult.Success(new DirectoryConnectionContext(connection));
    }

    /// <summary>
    /// Binds to the first reachable preferred domain controller. Failover semantics — which
    /// failures move on to the next controller and which stop immediately — live in
    /// <see cref="AdDirectoryFailoverPolicy"/> and are shared with connection validation.
    ///
    /// <para>
    /// The <paramref name="cancellationToken"/> is checked before each controller attempt, so a
    /// cancelled request stops immediately instead of spending the full bind timeout on every
    /// remaining controller. Callers must let the resulting
    /// <see cref="OperationCanceledException"/> propagate — every directory operation rethrows it
    /// ahead of its <c>LdapException</c>/<c>Exception</c> handlers, because a cancelled request is
    /// not a directory fault and must never surface as "domain controller unavailable" against a
    /// perfectly healthy domain.
    /// </para>
    /// </summary>
    private protected static LdapConnection CreateBoundConnection(
        DirectoryConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        var bindIdentity = AdServiceAccountBindIdentity.Build(
            context.Connection.ServiceAccountUserName,
            context.Connection.NetbiosDomainName);

        return AdDirectoryFailoverPolicy.BindWithFailover(
            ResolveOrderedHosts(context.Connection),
            host => CreateUnboundConnection(host, bindIdentity, context.Connection.ServiceAccountPassword),
            ldapConnection => ldapConnection.Bind(),
            cancellationToken);
    }

    internal static LdapConnection CreateUnboundConnection(
        string host,
        string bindIdentity,
        string? password)
    {
        var identifier = new LdapDirectoryIdentifier(host, LdapConnectionDefaults.StandardLdapsPort);
        var ldapConnection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Credential = new NetworkCredential(bindIdentity, password),
            Timeout = LdapOperationTimeout,
        };
        ldapConnection.SessionOptions.ProtocolVersion = 3;
        ldapConnection.SessionOptions.SecureSocketLayer = true;
        return ldapConnection;
    }

    private protected static string ResolvePrimaryHost(AdManagementConnectionParameters connection)
    {
        if (connection.PreferredDomainControllers.Count > 0
            && !string.IsNullOrWhiteSpace(connection.PreferredDomainControllers[0]))
        {
            return connection.PreferredDomainControllers[0];
        }

        return connection.DomainFqdn ?? string.Empty;
    }

    internal static IReadOnlyList<string> ResolveOrderedHosts(AdManagementConnectionParameters connection) =>
        AdDirectoryFailoverPolicy.ResolveOrderedHosts(connection);

    private protected static bool TryGetObjectGuid(SearchResultEntry entry, out Guid objectGuid)
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
        catch (Exception)
        {
            // Invalid objectGUID bytes are treated as missing attribute data.
            return false;
        }
    }

    private protected static string? GetFirstString(SearchResultEntry entry, string attributeName)
    {
        var attribute = TryGetAttribute(entry, attributeName);
        if (attribute is null || attribute.Count == 0)
        {
            return null;
        }

        return NormalizeOptional(GetRawValueAsString(attribute[0]));
    }

    private protected static IReadOnlyList<string> GetAllStrings(SearchResultEntry entry, string attributeName)
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

    private protected static int? GetFirstInt(SearchResultEntry entry, string attributeName)
    {
        var text = GetFirstString(entry, attributeName);
        return int.TryParse(text, out var value) ? value : null;
    }

    private protected static long? GetFirstLong(SearchResultEntry entry, string attributeName)
    {
        var text = GetFirstString(entry, attributeName);
        return long.TryParse(text, out var value) ? value : null;
    }

    private protected static byte[]? GetFirstBytes(SearchResultEntry entry, string attributeName)
    {
        var attribute = TryGetAttribute(entry, attributeName);
        if (attribute is null || attribute.Count == 0)
        {
            return null;
        }

        return attribute[0] as byte[];
    }

    private protected static DirectoryAttribute? TryGetAttribute(SearchResultEntry entry, string attributeName)
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

    private protected static string? GetRawValueAsString(object raw) =>
        raw switch
        {
            string text => text,
            byte[] bytes => DecodeLdapString(bytes),
            _ => raw.ToString(),
        };

    private protected static string? DecodeLdapString(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        try
        {
            return System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        catch (Exception)
        {
            // Invalid LDAP string encoding falls back to null attribute value.
            return null;
        }
    }

    private protected static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private protected void LogUnexpectedDirectoryFailure(
        Exception ex,
        Guid? actorUserId = null,
        [CallerMemberName] string operationName = "")
    {
        logger.LogError(
            ex,
            "AD directory operation unexpected failure. Operation={OperationName} ActorUserId={ActorUserId}",
            operationName,
            actorUserId);
    }

    private protected void LogBestEffortDirectoryFailure(
        Exception ex,
        [CallerMemberName] string operationName = "")
    {
        logger.LogWarning(
            ex,
            "AD directory operation best-effort step failed. Operation={OperationName}",
            operationName);
    }

    private protected static string BuildOrganizationalUnitSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return "(objectClass=organizationalUnit)";
        }

        var escaped = AdLdapFilterHelper.EscapeFilterValue(search.Trim());
        return
            $"(&(objectClass=organizationalUnit)(|(displayName=*{escaped}*)(name=*{escaped}*)(ou=*{escaped}*)(distinguishedName=*{escaped}*)))";
    }

    private protected static bool TryMapOrganizationalUnit(
        SearchResultEntry entry,
        out AdOrganizationalUnitListItem item)
    {
        item = null!;
        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var displayName = GetFirstString(entry, "displayName");
        var name = GetFirstString(entry, "name");
        var ou = GetFirstString(entry, "ou");
        string? objectGuid = null;
        if (TryGetObjectGuid(entry, out var guid))
        {
            objectGuid = guid.ToString("D");
        }

        item = new AdOrganizationalUnitListItem(
            distinguishedName,
            name,
            displayName,
            ou,
            AdOrganizationalUnitLabelBuilder.Build(distinguishedName, displayName, name, ou),
            objectGuid);
        return true;
    }

    private protected static bool TryLoadOrganizationalUnit(LdapConnection ldapConnection, string ouDistinguishedName)
    {
        var searchRequest = new SearchRequest(
            ouDistinguishedName.Trim(),
            "(objectClass=organizationalUnit)",
            SearchScope.Base,
            "distinguishedName")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        return response.ResultCode == ResultCode.Success && response.Entries.Count > 0;
    }

    private protected static string? ResolveRequiredGroupsSearchBase(AdManagementConnectionParameters connection) =>
        string.IsNullOrWhiteSpace(connection.GroupsSearchBase) ? null : connection.GroupsSearchBase.Trim();

    private protected static bool TryResolveManagedByDisplayName(
        LdapConnection ldapConnection,
        string managedByDistinguishedName,
        out string? displayName)
    {
        displayName = null;
        var searchRequest = new SearchRequest(
            managedByDistinguishedName.Trim(),
            "(|(objectClass=user)(objectClass=group)(objectClass=contact))",
            SearchScope.Base,
            "displayName",
            "cn",
            "name",
            "sAMAccountName")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        SearchResponse response;
        try
        {
            response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        }
        catch (LdapException)
        {
            return false;
        }

        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        var entry = response.Entries[0];
        displayName = GetFirstString(entry, "displayName")
            ?? GetFirstString(entry, "cn")
            ?? GetFirstString(entry, "name")
            ?? GetFirstString(entry, "sAMAccountName");
        return !string.IsNullOrWhiteSpace(displayName);
    }

    private protected static bool TryLoadGroupByDn(
        LdapConnection ldapConnection,
        string groupDistinguishedName,
        out AdGroupDirectoryInfo groupInfo)
    {
        groupInfo = null!;
        if (string.IsNullOrWhiteSpace(groupDistinguishedName))
        {
            return false;
        }

        var searchRequest = new SearchRequest(
            groupDistinguishedName.Trim(),
            "(objectClass=group)",
            SearchScope.Base,
            "distinguishedName",
            "displayName",
            "cn",
            "name",
            "sAMAccountName",
            "description")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        SearchResponse response;
        try
        {
            response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        }
        catch (LdapException)
        {
            return false;
        }

        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        return TryMapGroupDirectoryInfo(response.Entries[0], out groupInfo);
    }

    private protected static bool TryMapGroupDirectoryInfo(
        SearchResultEntry entry,
        out AdGroupDirectoryInfo groupInfo)
    {
        groupInfo = null!;
        var distinguishedName = GetFirstString(entry, "distinguishedName");
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            return false;
        }

        var name = GetFirstString(entry, "cn")
            ?? GetFirstString(entry, "name")
            ?? AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName)
            ?? distinguishedName;

        groupInfo = new AdGroupDirectoryInfo(
            distinguishedName,
            GetFirstString(entry, "displayName"),
            name,
            GetFirstString(entry, "sAMAccountName"),
            GetFirstString(entry, "description"));

        return true;
    }

    private protected sealed record AdGroupDirectoryInfo(
        string DistinguishedName,
        string? DisplayName,
        string Name,
        string? SamAccountName,
        string? Description);

    private protected sealed class DirectoryConnectionContext(AdManagementConnectionParameters connection)
    {
        public AdManagementConnectionParameters Connection { get; } = connection;
    }

    private protected sealed class ConnectionResolveResult
    {
        public bool IsSuccess { get; init; }
        public string MessageKey { get; init; } = string.Empty;
        public DirectoryConnectionContext? Context { get; init; }
        public AdDirectoryFailureKind? FailureKind { get; init; }
        public IReadOnlyDictionary<string, object>? MessageParams { get; init; }

        public static ConnectionResolveResult Success(DirectoryConnectionContext context) =>
            new() { IsSuccess = true, Context = context };

        public static ConnectionResolveResult Failed(
            string messageKey,
            AdDirectoryFailureKind kind,
            IReadOnlyDictionary<string, object>? messageParams = null) =>
            new()
            {
                IsSuccess = false,
                MessageKey = messageKey,
                FailureKind = kind,
                MessageParams = messageParams,
            };
    }
}
