using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string CreateUserFailedMessage = "AD kullanıcısı oluşturulamadı.";
    private const string NamingConflictFailedMessage =
        "Uygun kullanıcı adı veya UPN bulunamadı. Lütfen farklı bilgiler deneyin.";
    private const string PasswordSetFailedMessage = AdDirectoryConnectionRequirements.LdapsRequiredMessage;
    private const string InvalidTargetOuMessage =
        "Seçilen OU, AD yönetim ayarlarındaki kullanıcı kök OU altında olmalıdır.";
    private const string MissingUpnSuffixMessage = "UPN suffix seçimi zorunludur.";
    private const string InvalidUpnSuffixMessage = "UPN suffix geçerli bir domain suffix olmalıdır.";
    private const int OuSearchDefaultPageSize = 50;
    private const int OuSearchMaxPageSize = 100;
    private const int UserAccountControlDisabled = 0x0202;
    private const int UserAccountControlEnabled = 0x0200;
    private const string CreateUserSuccessLoggingFailedMessage =
        "AD user create operation succeeded but logging failed.";
    private const string CreateUserFailureLoggingFailedMessage =
        "AD user create operation failed but logging failed.";

    public async Task<AdOrganizationalUnitSearchResult> SearchOrganizationalUnitsAsync(
        AdOrganizationalUnitSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageSize = query.PageSize <= 0
            ? OuSearchDefaultPageSize
            : Math.Min(query.PageSize, OuSearchMaxPageSize);

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new AdOrganizationalUnitSearchResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind);
        }

        var usersRootOu = connectionResult.Context.Connection.UsersRootOu;
        if (string.IsNullOrWhiteSpace(usersRootOu))
        {
            return new AdOrganizationalUnitSearchResult(
                false,
                AdManagementNotConfiguredMessage,
                null,
                AdDirectoryFailureKind.NotConfigured);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var filter = BuildOrganizationalUnitSearchFilter(query.Search);
            var searchRequest = new SearchRequest(
                usersRootOu,
                filter,
                SearchScope.Subtree,
                "distinguishedName",
                "displayName",
                "name",
                "ou")
            {
                TimeLimit = LdapOperationTimeout,
            };

            var pageControl = new PageResultRequestControl(pageSize + 1);
            searchRequest.Controls.Add(pageControl);

            var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
            if (response.ResultCode != ResultCode.Success)
            {
                return OuConnectionFailed();
            }

            var items = new List<AdOrganizationalUnitListItem>();
            foreach (SearchResultEntry entry in response.Entries)
            {
                if (!TryMapOrganizationalUnit(entry, out var item))
                {
                    continue;
                }

                items.Add(item);
                if (items.Count > pageSize)
                {
                    break;
                }
            }

            var hasMore = items.Count > pageSize;
            if (hasMore)
            {
                items.RemoveAt(items.Count - 1);
            }

            return new AdOrganizationalUnitSearchResult(
                true,
                string.Empty,
                new AdOrganizationalUnitSearchPage(items, hasMore));
        }
        catch (LdapException)
        {
            return OuConnectionFailed();
        }
        catch (Exception)
        {
            return OuConnectionFailed();
        }
    }

    public async Task<CreateAdUserResult> CreateUserAsync(
        CreateAdUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateCreateRequest(request, out var validationMessage))
        {
            return new CreateAdUserResult(
                false,
                validationMessage,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var connectionResult = await ResolveConnectionAsync(cancellationToken);
        if (!connectionResult.IsSuccess || connectionResult.Context is null)
        {
            return new CreateAdUserResult(
                false,
                connectionResult.Message,
                null,
                connectionResult.FailureKind);
        }

        var connection = connectionResult.Context.Connection;

        var upnSuffix = AdDefaultUpnSuffixNormalizer.Normalize(request.UpnSuffix);
        if (string.IsNullOrWhiteSpace(upnSuffix) || !AdDefaultUpnSuffixNormalizer.IsValidFormat(upnSuffix))
        {
            return new CreateAdUserResult(
                false,
                InvalidUpnSuffixMessage,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var usersRootOu = connection.UsersRootOu;
        if (string.IsNullOrWhiteSpace(usersRootOu)
            || !AdLdapDnHelper.IsEqualOrDescendantOf(request.TargetOuDistinguishedName, usersRootOu))
        {
            return new CreateAdUserResult(
                false,
                InvalidTargetOuMessage,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var mappings = await attributeMappingService.GetMappingsAsync(cancellationToken);
        if (!AdCreateUserMappedAttributeValidator.TryValidate(
                request.MappedAttributes,
                mappings,
                out var mappedValidationMessage))
        {
            return new CreateAdUserResult(
                false,
                mappedValidationMessage,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

        try
        {
            using var ldapConnection = CreateBoundConnection(connectionResult.Context);
            var searchBase = connection.DefaultNamingContext ?? connection.BaseDn;
            if (string.IsNullOrWhiteSpace(searchBase))
            {
                return new CreateAdUserResult(
                    false,
                    AdManagementNotConfiguredMessage,
                    null,
                    AdDirectoryFailureKind.NotConfigured);
            }

            var resolvedNames = AdUserNamingCollisionResolver.Resolve(
                request.GivenName.Trim(),
                request.Surname.Trim(),
                request.SamAccountName,
                upnSuffix,
                candidate => HasNamingCollision(
                    ldapConnection,
                    searchBase,
                    request.TargetOuDistinguishedName,
                    candidate));

            if (resolvedNames is null)
            {
                await WriteCreateFailureLogsAsync(
                    request,
                    NamingConflictFailedMessage,
                    connection,
                    step: "ResolveNaming",
                    cancellationToken);
                return new CreateAdUserResult(
                    false,
                    NamingConflictFailedMessage,
                    null,
                    AdDirectoryFailureKind.InvalidRequest);
            }

            var distinguishedName = AdLdapDnHelper.BuildUserDistinguishedName(
                resolvedNames.CommonName,
                request.TargetOuDistinguishedName);

            var mappedAttributes = BuildMappedLdapAttributes(request.MappedAttributes, mappings);
            string? createdObjectGuid = null;

            try
            {
                ExecuteAddUser(
                    ldapConnection,
                    distinguishedName,
                    request,
                    resolvedNames,
                    mappedAttributes);

                createdObjectGuid = TryReadObjectGuidAfterCreate(ldapConnection, distinguishedName);

                SetUserPassword(ldapConnection, distinguishedName, request.InitialPassword);
                ApplyPasswordPolicyFlags(
                    ldapConnection,
                    distinguishedName,
                    request.MustChangePasswordAtNextLogon,
                    request.IsEnabled);
            }
            catch (CreateUserLdapException ex)
            {
                TryDeleteCreatedUser(ldapConnection, distinguishedName);
                await WriteCreateFailureLogsAsync(request, ex.UserMessage, connection, step: "CreateUser", cancellationToken);
                return new CreateAdUserResult(
                    false,
                    ex.UserMessage,
                    null,
                    ex.FailureKind);
            }

            var successMessage =
                $"Kullanıcı oluşturuldu: {resolvedNames.DisplayName} ({resolvedNames.SamAccountName}).";

            var responseWithoutNotifications = new CreateAdUserResponse(
                createdObjectGuid ?? distinguishedName,
                distinguishedName,
                resolvedNames.CommonName,
                resolvedNames.SamAccountName,
                resolvedNames.UserPrincipalName,
                resolvedNames.DisplayName,
                request.IsEnabled,
                successMessage,
                resolvedNames.NamingCollisionResolved,
                resolvedNames.GeneratedSuffix);

            var notificationSummary = await notificationEnqueueService.EnqueueUserCreatedAsync(
                new AdUserCreatedNotificationEnqueueRequest(
                    request,
                    responseWithoutNotifications,
                    mappings,
                    request.ActorUserName),
                cancellationToken);

            var response = responseWithoutNotifications with
            {
                NotificationSummary = notificationSummary,
            };

            await WriteCreateSuccessLogsAsync(
                request,
                response,
                connection,
                mappings,
                notificationSummary,
                cancellationToken);

            return new CreateAdUserResult(true, successMessage, response);
        }
        catch (LdapException)
        {
            await WriteCreateFailureLogsAsync(
                request,
                CreateUserFailedMessage,
                connectionResult.Context.Connection,
                step: "CreateUser",
                cancellationToken);
            return new CreateAdUserResult(
                false,
                CreateUserFailedMessage,
                null,
                AdDirectoryFailureKind.ConnectionFailed);
        }
        catch (Exception)
        {
            await WriteCreateFailureLogsAsync(
                request,
                CreateUserFailedMessage,
                connectionResult.Context.Connection,
                step: "CreateUser",
                cancellationToken);
            return new CreateAdUserResult(
                false,
                CreateUserFailedMessage,
                null,
                AdDirectoryFailureKind.ConnectionFailed);
        }
    }

    private static bool ValidateCreateRequest(CreateAdUserRequest request, out string message)
    {
        if (string.IsNullOrWhiteSpace(request.GivenName))
        {
            message = "Ad zorunludur.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Surname))
        {
            message = "Soyad zorunludur.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            message = "OU seçimi zorunludur.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.InitialPassword))
        {
            message = "İlk parola zorunludur.";
            return false;
        }

        var upnSuffix = AdDefaultUpnSuffixNormalizer.Normalize(request.UpnSuffix);
        if (string.IsNullOrWhiteSpace(upnSuffix) || !AdDefaultUpnSuffixNormalizer.IsValidFormat(upnSuffix))
        {
            message = InvalidUpnSuffixMessage;
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string BuildOrganizationalUnitSearchFilter(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return "(objectClass=organizationalUnit)";
        }

        var escaped = AdLdapFilterHelper.EscapeFilterValue(search.Trim());
        return
            $"(&(objectClass=organizationalUnit)(|(displayName=*{escaped}*)(name=*{escaped}*)(ou=*{escaped}*)(distinguishedName=*{escaped}*)))";
    }

    private static bool TryMapOrganizationalUnit(
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
        item = new AdOrganizationalUnitListItem(
            distinguishedName,
            name,
            displayName,
            ou,
            AdOrganizationalUnitLabelBuilder.Build(distinguishedName, displayName, name, ou));
        return true;
    }

    private static bool HasNamingCollision(
        LdapConnection ldapConnection,
        string domainSearchBase,
        string targetOuDistinguishedName,
        AdUserNamingCandidate candidate)
    {
        if (ExistsInOu(ldapConnection, targetOuDistinguishedName, "cn", candidate.CommonName))
        {
            return true;
        }

        if (ExistsInDomain(ldapConnection, domainSearchBase, "sAMAccountName", candidate.SamAccountName))
        {
            return true;
        }

        return ExistsInDomain(
            ldapConnection,
            domainSearchBase,
            "userPrincipalName",
            candidate.UserPrincipalName);
    }

    private static bool ExistsInOu(
        LdapConnection ldapConnection,
        string searchBase,
        string attributeName,
        string value)
    {
        var escapedValue = AdLdapFilterHelper.EscapeFilterValue(value);
        var filter =
            $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))({attributeName}={escapedValue}))";
        return Exists(ldapConnection, searchBase, filter);
    }

    private static bool ExistsInDomain(
        LdapConnection ldapConnection,
        string searchBase,
        string attributeName,
        string value)
    {
        var escapedValue = AdLdapFilterHelper.EscapeFilterValue(value);
        var filter =
            $"(&(objectCategory=person)(objectClass=user)(!(isDeleted=TRUE))({attributeName}={escapedValue}))";
        return Exists(ldapConnection, searchBase, filter);
    }

    private static bool Exists(LdapConnection ldapConnection, string searchBase, string filter)
    {
        var searchRequest = new SearchRequest(
            searchBase,
            filter,
            SearchScope.Subtree,
            "distinguishedName")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        return response.ResultCode == ResultCode.Success && response.Entries.Count > 0;
    }

    private static void ExecuteAddUser(
        LdapConnection ldapConnection,
        string distinguishedName,
        CreateAdUserRequest request,
        ResolvedAdUserNames resolvedNames,
        IReadOnlyList<DirectoryAttribute> mappedAttributes)
    {
        var addRequest = new AddRequest(distinguishedName);
        addRequest.Attributes.Add(new DirectoryAttribute("objectClass", "top", "person", "organizationalPerson", "user"));
        addRequest.Attributes.Add(new DirectoryAttribute("cn", resolvedNames.CommonName));
        addRequest.Attributes.Add(new DirectoryAttribute("name", resolvedNames.CommonName));
        addRequest.Attributes.Add(new DirectoryAttribute("givenName", request.GivenName.Trim()));
        addRequest.Attributes.Add(new DirectoryAttribute("sn", request.Surname.Trim()));
        addRequest.Attributes.Add(new DirectoryAttribute("displayName", resolvedNames.DisplayName));
        addRequest.Attributes.Add(new DirectoryAttribute("sAMAccountName", resolvedNames.SamAccountName));
        addRequest.Attributes.Add(new DirectoryAttribute("userPrincipalName", resolvedNames.UserPrincipalName));

        if (!string.IsNullOrWhiteSpace(request.Department))
        {
            addRequest.Attributes.Add(new DirectoryAttribute("department", request.Department.Trim()));
        }

        addRequest.Attributes.Add(new DirectoryAttribute("userAccountControl", UserAccountControlDisabled.ToString()));

        foreach (var attribute in mappedAttributes)
        {
            addRequest.Attributes.Add(attribute);
        }

        var response = (DirectoryResponse)ldapConnection.SendRequest(addRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new CreateUserLdapException(
                CreateUserFailedMessage,
                AdDirectoryFailureKind.ConnectionFailed);
        }
    }

    private static IReadOnlyList<DirectoryAttribute> BuildMappedLdapAttributes(
        IReadOnlyList<CreateAdUserMappedAttributeRequest> mappedAttributes,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var editableMappings = mappings
            .Where(static mapping => mapping.IsEnabled && mapping.IsEditable)
            .ToDictionary(static mapping => mapping.LogicalField, StringComparer.Ordinal);

        var result = new List<DirectoryAttribute>();
        foreach (var mappedAttribute in mappedAttributes)
        {
            if (!editableMappings.TryGetValue(mappedAttribute.LogicalField.Trim(), out var mapping))
            {
                continue;
            }

            var value = ExtractMappedAttributeValue(mappedAttribute.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result.Add(new DirectoryAttribute(mapping.AttributeName, value));
        }

        return result;
    }

    private static string? ExtractMappedAttributeValue(object? value) =>
        value switch
        {
            null => null,
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            IEnumerable<string> values => values.FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item))?.Trim(),
            _ => string.IsNullOrWhiteSpace(value.ToString()) ? null : value.ToString()!.Trim(),
        };

    private static void SetUserPassword(LdapConnection ldapConnection, string distinguishedName, string password)
    {
        if (!ldapConnection.SessionOptions.SecureSocketLayer)
        {
            throw new CreateUserLdapException(
                PasswordSetFailedMessage,
                AdDirectoryFailureKind.ConnectionFailed);
        }

        var quotedPassword = $"\"{password}\"";
        var passwordBytes = Encoding.Unicode.GetBytes(quotedPassword);
        var modifyRequest = new ModifyRequest(
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            "unicodePwd",
            passwordBytes);

        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new CreateUserLdapException(
                PasswordSetFailedMessage,
                AdDirectoryFailureKind.ConnectionFailed);
        }
    }

    private static void ApplyPasswordPolicyFlags(
        LdapConnection ldapConnection,
        string distinguishedName,
        bool mustChangePasswordAtNextLogon,
        bool isEnabled)
    {
        if (mustChangePasswordAtNextLogon)
        {
            var pwdLastSetRequest = new ModifyRequest(
                distinguishedName,
                DirectoryAttributeOperation.Replace,
                "pwdLastSet",
                "0");
            var pwdResponse = (DirectoryResponse)ldapConnection.SendRequest(pwdLastSetRequest);
            if (pwdResponse.ResultCode != ResultCode.Success)
            {
                throw new CreateUserLdapException(
                    CreateUserFailedMessage,
                    AdDirectoryFailureKind.ConnectionFailed);
            }
        }

        if (!isEnabled)
        {
            return;
        }

        var enableRequest = new ModifyRequest(
            distinguishedName,
            DirectoryAttributeOperation.Replace,
            "userAccountControl",
            UserAccountControlEnabled.ToString());
        var enableResponse = (DirectoryResponse)ldapConnection.SendRequest(enableRequest);
        if (enableResponse.ResultCode != ResultCode.Success)
        {
            throw new CreateUserLdapException(
                CreateUserFailedMessage,
                AdDirectoryFailureKind.ConnectionFailed);
        }
    }

    private static string? TryReadObjectGuidAfterCreate(LdapConnection ldapConnection, string distinguishedName)
    {
        var searchRequest = new SearchRequest(
            distinguishedName,
            "(objectClass=*)",
            SearchScope.Base,
            "objectGUID")
        {
            SizeLimit = 1,
            TimeLimit = LdapOperationTimeout,
        };

        var response = (SearchResponse)ldapConnection.SendRequest(searchRequest);
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return null;
        }

        return TryGetObjectGuid(response.Entries[0], out var objectGuid)
            ? objectGuid.ToString("D")
            : null;
    }

    private static void TryDeleteCreatedUser(LdapConnection ldapConnection, string distinguishedName)
    {
        try
        {
            var deleteRequest = new DeleteRequest(distinguishedName);
            ldapConnection.SendRequest(deleteRequest);
        }
        catch
        {
            // Best-effort rollback; original error is returned to caller.
        }
    }

    private async Task WriteCreateSuccessLogsAsync(
        CreateAdUserRequest request,
        CreateAdUserResponse response,
        AdManagementConnectionParameters connection,
        IReadOnlyList<AdAttributeMappingItem> mappings,
        AdManagementNotificationSummary _,
        CancellationToken cancellationToken)
    {
        var requestSummary = AdOperationLogSnapshotBuilder.BuildCreateRequestSummary(request);
        var afterSnapshot = AdOperationLogSnapshotBuilder.BuildCreateAfterSnapshot(
            response,
            request.IsEnabled,
            request.MappedAttributes,
            mappings);

        try
        {
            await adOperationLogService.WriteAsync(
                new AdOperationLogEntry
                {
                    OperationType = AdManagementOperationTypes.CreateUser,
                    Status = AdManagementOperationStatuses.Succeeded,
                    TargetObjectType = AdManagementTargetUserTypes.AdUser,
                    TargetDistinguishedName = response.DistinguishedName,
                    TargetObjectGuid = response.Id,
                    TargetSamAccountName = response.SamAccountName,
                    RequestSummaryJson = requestSummary,
                    BeforeSnapshotJson = null,
                    AfterSnapshotJson = afterSnapshot,
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                    DomainController = ResolvePrimaryHost(connection),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} SamAccountName={SamAccountName} UserId={UserId} ActorUserId={ActorUserId}",
                CreateUserSuccessLoggingFailedMessage,
                response.SamAccountName,
                response.Id,
                request.ActorUserId);
        }

        try
        {
            await auditLogWriter.WriteAsync(
                new AuditLogWriteRequest
                {
                    Action = "Create",
                    EntityName = "AdUser",
                    EntityId = response.Id,
                    Description = $"AD user created: {response.SamAccountName}.",
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} SamAccountName={SamAccountName} UserId={UserId} ActorUserId={ActorUserId}",
                CreateUserSuccessLoggingFailedMessage,
                response.SamAccountName,
                response.Id,
                request.ActorUserId);
        }
    }

    private async Task WriteCreateFailureLogsAsync(
        CreateAdUserRequest request,
        string message,
        AdManagementConnectionParameters connection,
        string step,
        CancellationToken cancellationToken)
    {
        var requestSummary = AdOperationLogSnapshotBuilder.BuildCreateRequestSummary(request);
        var diagnosticJson = AdOperationErrorDiagnosticBuilder.BuildCreateUserFailureJson(
            step,
            englishMessageOverride: ResolveCreateFailureEnglishMessage(message),
            normalizedReasonOverride: ResolveCreateFailureReason(message));

        try
        {
            await adOperationLogService.WriteAsync(
                new AdOperationLogEntry
                {
                    OperationType = AdManagementOperationTypes.CreateUser,
                    Status = AdManagementOperationStatuses.Failed,
                    TargetObjectType = AdManagementTargetUserTypes.AdUser,
                    TargetDistinguishedName = request.TargetOuDistinguishedName,
                    ErrorCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson),
                    ErrorMessage = diagnosticJson,
                    RequestSummaryJson = requestSummary,
                    BeforeSnapshotJson = null,
                    AfterSnapshotJson = null,
                    ActorUserId = request.ActorUserId,
                    ActorUserName = request.ActorUserName,
                    IpAddress = request.ActorIpAddress,
                    UserAgent = request.ActorUserAgent,
                    DomainController = ResolvePrimaryHost(connection),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{LogMessage} SamAccountName={SamAccountName} ActorUserId={ActorUserId}",
                CreateUserFailureLoggingFailedMessage,
                request.SamAccountName,
                request.ActorUserId);
        }
    }

    private static string? ResolveCreateFailureReason(string message)
    {
        if (string.Equals(message, NamingConflictFailedMessage, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.DuplicateValue;
        }

        if (string.Equals(message, PasswordSetFailedMessage, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.InvalidRequest;
        }

        if (string.Equals(message, CreateUserFailedMessage, StringComparison.Ordinal))
        {
            return AdUserUpdateNormalizedReasons.ConnectionFailed;
        }

        return AdUserUpdateNormalizedReasons.Unknown;
    }

    private static string ResolveCreateFailureEnglishMessage(string message) =>
        message switch
        {
            var value when string.Equals(value, NamingConflictFailedMessage, StringComparison.Ordinal) =>
                "A suitable samAccountName or UPN could not be resolved for the requested user.",
            var value when string.Equals(value, PasswordSetFailedMessage, StringComparison.Ordinal) =>
                "The initial password could not be set because LDAPS is required.",
            var value when string.Equals(value, CreateUserFailedMessage, StringComparison.Ordinal) =>
                "The AD user create operation failed.",
            _ => "The AD user create operation failed.",
        };

    private static AdOrganizationalUnitSearchResult OuConnectionFailed() =>
        new(false, DirectoryQueryFailedMessage, null, AdDirectoryFailureKind.ConnectionFailed);

    private sealed class CreateUserLdapException(string userMessage, AdDirectoryFailureKind failureKind)
        : Exception(userMessage)
    {
        public string UserMessage { get; } = userMessage;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
    }
}
