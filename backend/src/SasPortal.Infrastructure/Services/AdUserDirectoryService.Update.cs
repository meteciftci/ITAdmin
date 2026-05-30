using System.DirectoryServices.Protocols;
using System.Text.Json;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private const string UpdateUserFailedMessage = AdLdapErrorNormalizer.UpdateUserFailedMessage;

    public async Task<AdUserDirectoryDetailResult> UpdateUserAsync(
        UpdateAdUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeUpdateRequest(request);

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
        if (!AdUpdateUserRequestValidator.TryValidate(normalizedRequest, mappings, out var validationMessage))
        {
            return new AdUserDirectoryDetailResult(
                false,
                validationMessage,
                null,
                AdDirectoryFailureKind.InvalidRequest);
        }

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

        var activeMappings = mappings.Where(static mapping => mapping.IsEnabled).ToList();

        try
        {
            using var ldapConnection = CreateBoundConnection(context);
            if (!TryLoadUserForUpdate(
                    ldapConnection,
                    searchBase,
                    normalizedRequest.UserId,
                    activeMappings,
                    out var beforeDetail))
            {
                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    UserNotFoundMessage,
                    AdDirectoryFailureKind.NotFound,
                    beforeDetail,
                    beforeDetail?.DistinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            var distinguishedName = beforeDetail!.DistinguishedName;
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    UserNotFoundMessage,
                    AdDirectoryFailureKind.NotFound,
                    beforeDetail,
                    distinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            var newCommonName = AdUpdateUserRequestValidator.DeriveCommonNameFromDisplayName(normalizedRequest.DisplayName);
            var currentCommonName = AdLdapDnHelper.ParseCommonNameFromDistinguishedName(distinguishedName);
            try
            {
                if (!string.Equals(currentCommonName, newCommonName, StringComparison.OrdinalIgnoreCase))
                {
                    distinguishedName = RenameUserCommonName(
                        ldapConnection,
                        distinguishedName,
                        newCommonName);
                }

                ApplyUserAttributeUpdates(
                    ldapConnection,
                    distinguishedName,
                    normalizedRequest,
                    normalizedRequest.MappedAttributes,
                    mappings);
            }
            catch (UpdateUserLdapException ex)
            {
                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    ex.UserMessage,
                    ex.FailureKind,
                    beforeDetail,
                    distinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            if (!TryLoadUserForUpdate(
                    ldapConnection,
                    searchBase,
                    normalizedRequest.UserId,
                    activeMappings,
                    out var afterDetail)
                || afterDetail is null)
            {
                return await FailUpdateAsync(
                    normalizedRequest,
                    context.Connection,
                    UpdateUserFailedMessage,
                    AdDirectoryFailureKind.ConnectionFailed,
                    beforeDetail,
                    distinguishedName,
                    null,
                    null,
                    cancellationToken);
            }

            await WriteUpdateSuccessLogsAsync(
                normalizedRequest,
                context.Connection,
                beforeDetail,
                afterDetail,
                distinguishedName,
                cancellationToken);

            return new AdUserDirectoryDetailResult(true, string.Empty, afterDetail);
        }
        catch (LdapException ex)
        {
            return await FailUpdateAsync(
                normalizedRequest,
                context.Connection,
                AdLdapErrorNormalizer.Normalize(ex.ErrorCode, ex.Message),
                AdDirectoryFailureKind.ConnectionFailed,
                null,
                null,
                null,
                null,
                cancellationToken);
        }
        catch (Exception)
        {
            return await FailUpdateAsync(
                normalizedRequest,
                context.Connection,
                UpdateUserFailedMessage,
                AdDirectoryFailureKind.ConnectionFailed,
                null,
                null,
                null,
                null,
                cancellationToken);
        }
    }

    private static UpdateAdUserRequest NormalizeUpdateRequest(UpdateAdUserRequest request) =>
        request with
        {
            GivenName = request.GivenName.Trim(),
            Surname = request.Surname.Trim(),
            DisplayName = request.DisplayName.Trim(),
            SamAccountName = request.SamAccountName.Trim(),
            UserPrincipalName = request.UserPrincipalName.Trim(),
            Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
            Mail = request.Mail is null ? null : (string.IsNullOrWhiteSpace(request.Mail) ? string.Empty : request.Mail.Trim()),
            MappedAttributes = request.MappedAttributes
                .Select(static attribute => new UpdateAdUserMappedAttributeRequest(
                    attribute.LogicalField.Trim(),
                    attribute.Value))
                .ToList(),
        };

    private static bool TryLoadUserForUpdate(
        LdapConnection ldapConnection,
        string searchBase,
        Guid objectGuid,
        IReadOnlyList<AdAttributeMappingItem> activeMappings,
        out AdUserDetail? detail)
    {
        detail = null;
        var guidFilter = AdLdapFilterHelper.FormatObjectGuidFilter(objectGuid);
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
        if (response.ResultCode != ResultCode.Success || response.Entries.Count == 0)
        {
            return false;
        }

        return TryMapDetailItem(response.Entries[0], activeMappings, out detail);
    }

    private static string RenameUserCommonName(
        LdapConnection ldapConnection,
        string distinguishedName,
        string newCommonName)
    {
        var parentDn = AdLdapDnHelper.GetParentDistinguishedName(distinguishedName);
        if (string.IsNullOrWhiteSpace(parentDn))
        {
            throw new UpdateUserLdapException(
                AdLdapErrorNormalizer.InvalidDnSyntaxMessage,
                AdDirectoryFailureKind.InvalidRequest);
        }

        var newRdn = AdLdapDnHelper.BuildCommonNameRdn(newCommonName);
        var modifyDnRequest = new ModifyDNRequest(distinguishedName, parentDn, newRdn);
        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyDnRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new UpdateUserLdapException(
                AdLdapErrorNormalizer.Normalize((int)response.ResultCode, response.ErrorMessage),
                MapFailureKind(response.ResultCode));
        }

        return AdLdapDnHelper.BuildUserDistinguishedName(newCommonName, parentDn);
    }

    private static void ApplyUserAttributeUpdates(
        LdapConnection ldapConnection,
        string distinguishedName,
        UpdateAdUserRequest request,
        IReadOnlyList<UpdateAdUserMappedAttributeRequest> mappedAttributes,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var commonName = AdUpdateUserRequestValidator.DeriveCommonNameFromDisplayName(request.DisplayName);

        ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Replace, "givenName", request.GivenName);
        ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Replace, "sn", request.Surname);
        ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Replace, "displayName", request.DisplayName);
        ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Replace, "name", commonName);
        ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Replace, "sAMAccountName", request.SamAccountName);
        ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Replace, "userPrincipalName", request.UserPrincipalName);

        if (request.Mail is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Mail))
            {
                ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Delete, "mail");
            }
            else
            {
                ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Replace, "mail", request.Mail);
            }
        }

        if (request.Department is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Department))
            {
                ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Delete, "department");
            }
            else
            {
                ApplyLdapModification(ldapConnection, distinguishedName, DirectoryAttributeOperation.Replace, "department", request.Department);
            }
        }

        ApplyMappedAttributeUpdates(ldapConnection, distinguishedName, mappedAttributes, mappings);
    }

    private static void ApplyMappedAttributeUpdates(
        LdapConnection ldapConnection,
        string distinguishedName,
        IReadOnlyList<UpdateAdUserMappedAttributeRequest> mappedAttributes,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var editableMappings = mappings
            .Where(static mapping => mapping.IsEnabled && mapping.IsEditable)
            .ToDictionary(static mapping => mapping.LogicalField, StringComparer.Ordinal);

        foreach (var mappedAttribute in mappedAttributes)
        {
            if (!editableMappings.TryGetValue(mappedAttribute.LogicalField, out var mapping))
            {
                continue;
            }

            var value = ExtractMappedAttributeValue(mappedAttribute.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                ApplyLdapModification(
                    ldapConnection,
                    distinguishedName,
                    DirectoryAttributeOperation.Delete,
                    mapping.AttributeName);
            }
            else
            {
                ApplyLdapModification(
                    ldapConnection,
                    distinguishedName,
                    DirectoryAttributeOperation.Replace,
                    mapping.AttributeName,
                    value);
            }
        }
    }

    private static void ApplyLdapModification(
        LdapConnection ldapConnection,
        string distinguishedName,
        DirectoryAttributeOperation operation,
        string attributeName,
        params string[] values)
    {
        var modifyRequest = values.Length == 0
            ? new ModifyRequest(distinguishedName, operation, attributeName)
            : new ModifyRequest(distinguishedName, operation, attributeName, values);

        var response = (DirectoryResponse)ldapConnection.SendRequest(modifyRequest);
        if (response.ResultCode != ResultCode.Success)
        {
            throw new UpdateUserLdapException(
                AdLdapErrorNormalizer.Normalize((int)response.ResultCode, response.ErrorMessage),
                MapFailureKind(response.ResultCode));
        }
    }

    private static AdDirectoryFailureKind MapFailureKind(ResultCode resultCode) =>
        resultCode switch
        {
            ResultCode.NoSuchObject => AdDirectoryFailureKind.NotFound,
            ResultCode.EntryAlreadyExists
                or ResultCode.AttributeOrValueExists
                or ResultCode.ConstraintViolation
                or ResultCode.InvalidDNSyntax
                or ResultCode.NamingViolation
                or ResultCode.UnwillingToPerform => AdDirectoryFailureKind.InvalidRequest,
            _ => AdDirectoryFailureKind.ConnectionFailed,
        };

    private async Task<AdUserDirectoryDetailResult> FailUpdateAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        string message,
        AdDirectoryFailureKind failureKind,
        AdUserDetail? beforeDetail,
        string? targetDistinguishedName,
        AdUserDetail? afterDetail,
        string? afterDistinguishedName,
        CancellationToken cancellationToken)
    {
        await WriteUpdateFailureLogsAsync(
            request,
            connection,
            beforeDetail,
            targetDistinguishedName,
            afterDetail,
            afterDistinguishedName,
            message,
            cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = "Update",
                EntityName = "AdUser",
                EntityId = request.UserId.ToString("D"),
                Description = $"AD user update failed: {request.SamAccountName}.",
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
            },
            cancellationToken);

        return new AdUserDirectoryDetailResult(false, message, null, failureKind);
    }

    private async Task WriteUpdateSuccessLogsAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        AdUserDetail beforeDetail,
        AdUserDetail afterDetail,
        string targetDistinguishedName,
        CancellationToken cancellationToken)
    {
        await WriteUpdateOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Succeeded,
            beforeDetail,
            targetDistinguishedName,
            afterDetail,
            afterDetail.DistinguishedName,
            null,
            cancellationToken);

        await auditLogWriter.WriteAsync(
            new AuditLogWriteRequest
            {
                Action = "Update",
                EntityName = "AdUser",
                EntityId = afterDetail.Id,
                Description = $"AD user updated: {afterDetail.SamAccountName}.",
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
            },
            cancellationToken);
    }

    private Task WriteUpdateFailureLogsAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        AdUserDetail? beforeDetail,
        string? targetDistinguishedName,
        AdUserDetail? afterDetail,
        string? afterDistinguishedName,
        string message,
        CancellationToken cancellationToken) =>
        WriteUpdateOperationLogAsync(
            request,
            connection,
            AdManagementOperationStatuses.Failed,
            beforeDetail,
            targetDistinguishedName,
            afterDetail,
            afterDistinguishedName,
            message,
            cancellationToken);

    private async Task WriteUpdateOperationLogAsync(
        UpdateAdUserRequest request,
        AdManagementConnectionParameters connection,
        string status,
        AdUserDetail? beforeDetail,
        string? targetDistinguishedName,
        AdUserDetail? afterDetail,
        string? afterDistinguishedName,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var beforeSnapshot = beforeDetail is null ? null : SerializeUpdateSnapshot(beforeDetail);
        var afterSnapshot = afterDetail is null ? null : SerializeUpdateSnapshot(afterDetail);

        await adOperationLogService.WriteAsync(
            new AdOperationLogEntry
            {
                OperationType = AdManagementOperationTypes.UserUpdate,
                Status = status,
                TargetObjectType = AdManagementTargetUserTypes.AdUser,
                TargetObjectGuid = afterDetail?.Id ?? beforeDetail?.Id ?? request.UserId.ToString("D"),
                TargetDistinguishedName = afterDistinguishedName
                    ?? targetDistinguishedName
                    ?? beforeDetail?.DistinguishedName,
                TargetSamAccountName = afterDetail?.SamAccountName ?? beforeDetail?.SamAccountName ?? request.SamAccountName,
                BeforeSnapshotJson = beforeSnapshot,
                AfterSnapshotJson = afterSnapshot,
                ErrorMessage = errorMessage,
                ActorUserId = request.ActorUserId,
                ActorUserName = request.ActorUserName,
                IpAddress = request.ActorIpAddress,
                UserAgent = request.ActorUserAgent,
                DomainController = ResolvePrimaryHost(connection),
            },
            cancellationToken);
    }

    private static string SerializeUpdateSnapshot(AdUserDetail detail) =>
        JsonSerializer.Serialize(AdUserUpdateSnapshotBuilder.Build(
            detail.GivenName,
            detail.Surname,
            detail.DisplayName,
            detail.SamAccountName,
            detail.UserPrincipalName,
            detail.Mail,
            detail.Department,
            detail.DistinguishedName,
            detail.MappedAttributes));

    private sealed class UpdateUserLdapException(string userMessage, AdDirectoryFailureKind failureKind)
        : Exception(userMessage)
    {
        public string UserMessage { get; } = userMessage;
        public AdDirectoryFailureKind FailureKind { get; } = failureKind;
    }
}
