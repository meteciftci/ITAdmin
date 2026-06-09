using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Domain.Entities;

namespace SasPortal.Application.Common.AdManagement;

public static class AdOperationLogSnapshotBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions SerializerOptionsWithNullManager = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildGroupMembershipRequestSummary(
        string operationType,
        Guid userId,
        string groupDistinguishedName,
        string? groupName = null) =>
        JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                userId = userId.ToString("D"),
                groupDistinguishedName,
                groupName,
            },
            SerializerOptions);

    public static string BuildGroupMembershipBeforeSnapshot(
        string operationType,
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        string? groupName,
        string? groupDistinguishedName,
        bool isDirectMember) =>
        JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                group = BuildGroupSnapshot(groupName, groupDistinguishedName),
                membership = new { isDirectMember },
            },
            SerializerOptions);

    public static string BuildGroupMembershipAfterSnapshot(
        string operationType,
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        string? groupName,
        string? groupDistinguishedName,
        bool isDirectMember) =>
        JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                group = BuildGroupSnapshot(groupName, groupDistinguishedName),
                membership = new { isDirectMember },
            },
            SerializerOptions);

    public static string BuildGroupMemberOperationRequestSummary(
        string operationType,
        string groupId,
        string? groupName,
        string? groupSamAccountName,
        string? groupDistinguishedName,
        string memberType,
        string? memberName,
        string? memberSamAccountName,
        string memberDistinguishedName) =>
        JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                groupId,
                groupName,
                groupSamAccountName,
                groupDistinguishedName,
                memberType,
                memberName,
                memberSamAccountName,
                memberDistinguishedName,
            },
            SerializerOptions);

    public static string BuildGroupMemberOperationBeforeSnapshot(
        string operationType,
        AdGroupDetail group,
        AdGroupMemberSnapshotInfo member,
        bool isDirectMember) =>
        JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                group = CreateGroupSnapshotBody(group),
                member = CreateMemberSnapshotBody(member),
                membership = new { isDirectMember },
            },
            SerializerOptions);

    public static string BuildGroupMemberOperationAfterSnapshot(
        string operationType,
        AdGroupDetail group,
        AdGroupMemberSnapshotInfo member,
        bool isDirectMember) =>
        JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                group = CreateGroupSnapshotBody(group),
                member = CreateMemberSnapshotBody(member),
                membership = new { isDirectMember },
            },
            SerializerOptions);

    internal static object CreateMemberSnapshotBody(AdGroupMemberSnapshotInfo member) =>
        new
        {
            id = member.Id,
            type = member.Type,
            displayName = member.DisplayName,
            name = member.Name,
            cn = member.Cn,
            samAccountName = member.SamAccountName,
            userPrincipalName = member.UserPrincipalName,
            dNSHostName = member.DNSHostName,
            description = member.Description,
            distinguishedName = member.DistinguishedName,
        };

    public static string BuildUserOuMoveRequestSummary(
        Guid userId,
        string targetOuDistinguishedName) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.UserOuMove,
                userId = userId.ToString("D"),
                targetOuDistinguishedName,
            },
            SerializerOptions);

    public static string BuildUserOuMoveBeforeSnapshot(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        string? parentOuDistinguishedName) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.UserOuMove,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                ou = new { distinguishedName = parentOuDistinguishedName },
            },
            SerializerOptions);

    public static string BuildUserOuMoveAfterSnapshot(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        string? parentOuDistinguishedName) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.UserOuMove,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                ou = new { distinguishedName = parentOuDistinguishedName },
            },
            SerializerOptions);

    public static string BuildGroupOuMoveRequestSummary(
        Guid groupId,
        string? sourceDistinguishedName,
        string targetOuDistinguishedName) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.GroupMoveOu,
                groupId = groupId.ToString("D"),
                sourceDistinguishedName,
                targetOuDistinguishedName,
            },
            SerializerOptions);

    public static string BuildGroupOuMoveBeforeSnapshot(AdGroupDetail group, string? parentOuDistinguishedName) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.GroupMoveOu,
                group = CreateGroupSnapshotBody(group),
                ou = new { distinguishedName = parentOuDistinguishedName },
            },
            SerializerOptions);

    public static string BuildGroupOuMoveAfterSnapshot(AdGroupDetail group, string? parentOuDistinguishedName) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.GroupMoveOu,
                group = CreateGroupSnapshotBody(group),
                ou = new { distinguishedName = parentOuDistinguishedName },
            },
            SerializerOptions);

    public static string BuildUserManagerUpdateRequestSummary(
        Guid userId,
        Guid? managerUserId,
        bool clearManager) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.UserManagerUpdate,
                userId = userId.ToString("D"),
                managerUserId = managerUserId?.ToString("D"),
                clearManager,
            },
            SerializerOptions);

    public static string BuildUserManagerUpdateBeforeSnapshot(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        AdUserManagerSnapshotInfo? manager) =>
        JsonSerializer.Serialize(
            BuildManagerUpdateSnapshotBody(userId, samAccountName, userPrincipalName, distinguishedName, manager),
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

    public static string BuildUserManagerUpdateAfterSnapshot(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        AdUserManagerSnapshotInfo? manager) =>
        JsonSerializer.Serialize(
            BuildManagerUpdateSnapshotBody(userId, samAccountName, userPrincipalName, distinguishedName, manager),
            SerializerOptionsWithNullManager);

    public static string BuildUserAccountExpirationUpdateRequestSummary(
        Guid userId,
        bool neverExpires,
        string? expiresAt) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.UserAccountExpirationUpdate,
                userId = userId.ToString("D"),
                neverExpires,
                expiresAt,
            },
            SerializerOptions);

    public static string BuildUserAccountExpirationUpdateBeforeSnapshot(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        bool neverExpires,
        string? accountExpiresDate) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.UserAccountExpirationUpdate,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                accountExpiration = new
                {
                    neverExpires,
                    accountExpiresDate,
                },
            },
            SerializerOptions);

    public static string BuildUserAccountExpirationUpdateAfterSnapshot(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        bool neverExpires,
        string? accountExpiresDate) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.UserAccountExpirationUpdate,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                accountExpiration = new
                {
                    neverExpires,
                    accountExpiresDate,
                },
            },
            SerializerOptions);

    public static string BuildAccountRequestSummary(
        string operationType,
        Guid userId,
        bool? requestedEnabled = null)
    {
        if (requestedEnabled is null)
        {
            return JsonSerializer.Serialize(
                new { operation = operationType, userId = userId.ToString("D") },
                SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                userId = userId.ToString("D"),
                requestedEnabled,
            },
            SerializerOptions);
    }

    public static string BuildAccountBeforeSnapshot(
        string operationType,
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        bool isEnabled,
        bool isLockedOut,
        int? userAccountControl,
        long? lockoutTime)
    {
        if (operationType == AdManagementOperationTypes.UserUnlock)
        {
            return JsonSerializer.Serialize(
                new
                {
                    operation = operationType,
                    user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                    account = new
                    {
                        isLocked = isLockedOut,
                        lockoutTime = FormatLockoutTime(lockoutTime),
                    },
                },
                SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                account = new
                {
                    isEnabled,
                    isLocked = isLockedOut,
                    userAccountControl,
                },
            },
            SerializerOptions);
    }

    public static string BuildAccountAfterSnapshot(
        string operationType,
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        bool isEnabled,
        bool isLockedOut,
        int? userAccountControl,
        long? lockoutTime)
    {
        if (operationType == AdManagementOperationTypes.UserUnlock)
        {
            return JsonSerializer.Serialize(
                new
                {
                    operation = operationType,
                    user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                    account = new
                    {
                        isLocked = isLockedOut,
                        lockoutTime = FormatLockoutTime(lockoutTime),
                    },
                },
                SerializerOptions);
        }

        return JsonSerializer.Serialize(
            new
            {
                operation = operationType,
                user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
                account = new
                {
                    isEnabled,
                    isLocked = isLockedOut,
                    userAccountControl,
                },
            },
            SerializerOptions);
    }

    public static string BuildSettingsSnapshot(
        AdManagementSettings entity,
        IReadOnlyList<string> preferredDomainControllers,
        AdManagementNotificationSettings notificationSettings) =>
        JsonSerializer.Serialize(
            new
            {
                isEnabled = entity.IsEnabled,
                domainFqdn = entity.DomainFqdn,
                defaultUserCreationUpnSuffix = entity.DefaultUserCreationUpnSuffix,
                netbiosDomainName = entity.NetbiosDomainName,
                defaultNamingContext = entity.DefaultNamingContext,
                baseDn = entity.BaseDn,
                usersRootOu = entity.UsersRootOu,
                disabledUsersOu = entity.DisabledUsersOu,
                groupsSearchBase = entity.GroupsSearchBase,
                computersSearchBase = entity.ComputersSearchBase,
                preferredDomainControllers,
                useSsl = entity.UseSsl,
                ldapPort = entity.LdapPort,
                serviceAccountUserName = entity.ServiceAccountUserName,
                hasServiceAccountPassword = !string.IsNullOrWhiteSpace(entity.EncryptedServiceAccountPassword),
                powerShellHealthEnabled = entity.PowerShellHealthEnabled,
                powerShellTimeoutSeconds = entity.PowerShellTimeoutSeconds,
                notificationSettings = BuildNotificationSettingsSummary(notificationSettings),
                lastValidatedAt = entity.LastValidatedAt,
                lastValidationStatus = entity.LastValidationStatus,
            },
            SerializerOptions);

    public static string BuildSettingsUpdatedRequestSummary(
        UpdateAdManagementSettingsRequest request,
        AdManagementSettings entity,
        IReadOnlyList<string> preferredDomainControllers,
        bool passwordChanged,
        bool notificationRulesChanged,
        AdManagementSettings? beforeEntity = null)
    {
        var changedFields = beforeEntity is null
            ? null
            : BuildSettingsChangedFields(beforeEntity, request, passwordChanged, notificationRulesChanged);

        return JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.SettingsUpdated,
                isEnabled = request.IsEnabled,
                domainFqdn = entity.DomainFqdn,
                defaultUserCreationUpnSuffix = entity.DefaultUserCreationUpnSuffix,
                netbiosDomainName = entity.NetbiosDomainName,
                useSsl = entity.UseSsl,
                ldapPort = entity.LdapPort,
                preferredDomainControllers,
                passwordChanged,
                passwordCleared = request.ClearServiceAccountPassword,
                notificationRulesChanged,
                changedFields,
            },
            SerializerOptions);
    }

    public static string BuildSettingsValidationSummary(AdManagementValidationResult result) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.SettingsValidated,
                isValid = result.IsValid,
                checkedAt = result.CheckedAt,
                message = SanitizeValidationMessage(result.Message),
                details = result.Details
                    .Select(static detail => new
                    {
                        key = detail.Key,
                        status = detail.Status,
                        message = SanitizeValidationMessage(detail.Message),
                    })
                    .ToList(),
            },
            SerializerOptions);

    public static string BuildAttributeMappingSnapshot(AdAttributeMapping entity) =>
        JsonSerializer.Serialize(
            new
            {
                id = entity.Id,
                logicalField = entity.LogicalField,
                displayName = entity.DisplayName,
                attributeName = entity.AttributeName,
                isEnabled = entity.IsEnabled,
                isEditable = entity.IsEditable,
                isSensitive = entity.IsSensitive,
                isSearchable = entity.IsSearchable,
                validationType = entity.ValidationType,
                maskingStrategy = entity.MaskingStrategy,
                sortOrder = entity.SortOrder,
            },
            SerializerOptions);

    public static string BuildAttributeMappingCreateRequestSummary(CreateAdAttributeMappingRequest request) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.AttributeMappingCreated,
                logicalField = NormalizeNullable(request.LogicalField),
                displayName = NormalizeNullable(request.DisplayName),
                attributeName = NormalizeNullable(request.AttributeName),
                isEnabled = request.IsEnabled,
                isEditable = request.IsEditable,
                isSensitive = request.IsSensitive,
                isSearchable = request.IsSearchable,
                validationType = NormalizeNullable(request.ValidationType),
                maskingStrategy = NormalizeNullable(request.MaskingStrategy),
                sortOrder = request.SortOrder,
            },
            SerializerOptions);

    public static string BuildAttributeMappingUpdateRequestSummary(
        UpdateAdAttributeMappingRequest request,
        AdAttributeMapping before)
    {
        var displayName = NormalizeNullable(request.DisplayName);
        var attributeName = NormalizeNullable(request.AttributeName);
        var validationType = NormalizeNullable(request.ValidationType) ?? "None";
        var maskingStrategy = NormalizeNullable(request.MaskingStrategy) ?? "None";

        var changedFields = new List<string>();
        if (!string.Equals(before.DisplayName, displayName, StringComparison.Ordinal))
        {
            changedFields.Add("displayName");
        }

        if (!string.Equals(before.AttributeName, attributeName, StringComparison.Ordinal))
        {
            changedFields.Add("attributeName");
        }

        if (before.IsEnabled != request.IsEnabled)
        {
            changedFields.Add("isEnabled");
        }

        if (before.IsEditable != request.IsEditable)
        {
            changedFields.Add("isEditable");
        }

        if (before.IsSensitive != request.IsSensitive)
        {
            changedFields.Add("isSensitive");
        }

        if (before.IsSearchable != request.IsSearchable)
        {
            changedFields.Add("isSearchable");
        }

        if (!string.Equals(before.ValidationType, validationType, StringComparison.Ordinal))
        {
            changedFields.Add("validationType");
        }

        if (!string.Equals(before.MaskingStrategy, maskingStrategy, StringComparison.Ordinal))
        {
            changedFields.Add("maskingStrategy");
        }

        if (before.SortOrder != request.SortOrder)
        {
            changedFields.Add("sortOrder");
        }

        return JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.AttributeMappingUpdated,
                id = request.Id,
                changedFields,
                displayName,
                attributeName,
                isEnabled = request.IsEnabled,
                isEditable = request.IsEditable,
                isSensitive = request.IsSensitive,
                isSearchable = request.IsSearchable,
                validationType,
                maskingStrategy,
                sortOrder = request.SortOrder,
            },
            SerializerOptions);
    }

    public static string BuildAttributeMappingDeleteRequestSummary(
        DeleteAdAttributeMappingRequest request,
        AdAttributeMapping entity) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.AttributeMappingDeleted,
                id = request.Id,
                logicalField = entity.LogicalField,
            },
            SerializerOptions);

    public static string BuildGroupCreateRequestSummary(CreateAdGroupRequest request) =>
        JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.GroupCreate,
                displayName = request.DisplayName.Trim(),
                name = request.Name.Trim(),
                samAccountName = request.SamAccountName.Trim(),
                description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                groupScope = request.GroupScope.Trim(),
                targetOuDistinguishedName = request.TargetOuDistinguishedName.Trim(),
            },
            SerializerOptions);

    public static string BuildGroupOperationSnapshot(string operationType, AdGroupDetail? group) =>
        group is null
            ? "{}"
            : JsonSerializer.Serialize(
                new
                {
                    operation = operationType,
                    group = CreateGroupSnapshotBody(group),
                },
                SerializerOptions);

    public static string BuildGroupCreateAfterSnapshot(AdGroupDetail group) =>
        BuildGroupOperationSnapshot(AdManagementOperationTypes.GroupCreate, group);

    internal static object CreateGroupSnapshotBody(AdGroupDetail group) =>
        new
        {
            id = group.Id,
            displayName = group.DisplayName,
            name = group.Name,
            cn = group.Cn,
            samAccountName = group.SamAccountName,
            description = group.Description,
            distinguishedName = group.DistinguishedName,
            groupScope = group.GroupScope,
            securityEnabled = group.SecurityEnabled,
            groupType = group.GroupType,
            memberCount = group.MemberCount,
            memberOfCount = group.MemberOfCount,
        };

    public static string BuildCreateRequestSummary(CreateAdUserRequest request)
    {
        var mappedAttributeFields = request.MappedAttributes
            .Select(static attribute => attribute.LogicalField.Trim())
            .Where(static field => !string.IsNullOrWhiteSpace(field))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static field => field, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.CreateUser,
                givenName = request.GivenName.Trim(),
                surname = request.Surname.Trim(),
                samAccountName = string.IsNullOrWhiteSpace(request.SamAccountName)
                    ? null
                    : request.SamAccountName.Trim(),
                upnSuffix = request.UpnSuffix.Trim(),
                targetOuDistinguishedName = request.TargetOuDistinguishedName.Trim(),
                isEnabled = request.IsEnabled,
                mustChangePasswordAtNextLogon = request.MustChangePasswordAtNextLogon,
                mappedAttributeFields,
            },
            SerializerOptions);
    }

    public static string BuildCreateAfterSnapshot(
        CreateAdUserResponse response,
        bool isEnabled,
        IReadOnlyList<CreateAdUserMappedAttributeRequest> mappedAttributeRequests,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var mappedAttributes = BuildCreateMappedAttributesForSnapshot(mappedAttributeRequests, mappings);

        return JsonSerializer.Serialize(
            new
            {
                operation = AdManagementOperationTypes.CreateUser,
                user = new
                {
                    id = response.Id,
                    samAccountName = response.SamAccountName,
                    userPrincipalName = response.UserPrincipalName,
                    displayName = response.DisplayName,
                    distinguishedName = response.DistinguishedName,
                },
                account = new { isEnabled },
                mappedAttributes,
            },
            SerializerOptions);
    }

    private static IReadOnlyList<object> BuildCreateMappedAttributesForSnapshot(
        IReadOnlyList<CreateAdUserMappedAttributeRequest> mappedAttributeRequests,
        IReadOnlyList<AdAttributeMappingItem> mappings)
    {
        var mappingByField = mappings
            .Where(static mapping => mapping.IsEnabled)
            .ToDictionary(static mapping => mapping.LogicalField, StringComparer.OrdinalIgnoreCase);

        var result = new List<object>();
        foreach (var mappedAttribute in mappedAttributeRequests)
        {
            var logicalField = mappedAttribute.LogicalField.Trim();
            if (string.IsNullOrWhiteSpace(logicalField))
            {
                continue;
            }

            if (!mappingByField.TryGetValue(logicalField, out var mapping))
            {
                continue;
            }

            var rawValue = ExtractMappedAttributeValue(mappedAttribute.Value);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            var maskedValues = AdAttributeValueMasker.MaskValues(
                [rawValue],
                mapping.IsSensitive,
                mapping.MaskingStrategy);

            result.Add(new
            {
                logicalField,
                values = maskedValues,
            });
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

    private static object BuildUserSnapshot(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName) =>
        new
        {
            id = userId,
            samAccountName,
            userPrincipalName,
            distinguishedName,
        };

    private static object BuildGroupSnapshot(string? groupName, string? groupDistinguishedName) =>
        new
        {
            name = groupName,
            distinguishedName = groupDistinguishedName,
        };

    private static object? BuildManagerSnapshot(AdUserManagerSnapshotInfo? manager) =>
        manager is null
            ? null
            : new
            {
                id = manager.Id,
                samAccountName = manager.SamAccountName,
                userPrincipalName = manager.UserPrincipalName,
                displayName = manager.DisplayName,
                distinguishedName = manager.DistinguishedName,
            };

    private static object BuildManagerUpdateSnapshotBody(
        string userId,
        string? samAccountName,
        string? userPrincipalName,
        string? distinguishedName,
        AdUserManagerSnapshotInfo? manager) =>
        new
        {
            operation = AdManagementOperationTypes.UserManagerUpdate,
            user = BuildUserSnapshot(userId, samAccountName, userPrincipalName, distinguishedName),
            manager = BuildManagerSnapshot(manager),
        };


    private static string? FormatLockoutTime(long? lockoutTime) =>
        lockoutTime is null or 0 ? null : lockoutTime.Value.ToString();

    private static object BuildNotificationSettingsSummary(AdManagementNotificationSettings settings) =>
        new
        {
            rules = settings.Rules
                .OrderBy(static rule => rule.EventKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static rule => rule.Channel, StringComparer.OrdinalIgnoreCase)
                .Select(static rule => new
                {
                    id = rule.Id,
                    eventKey = rule.EventKey,
                    channel = rule.Channel,
                    isEnabled = rule.IsEnabled,
                    recipientSourceType = rule.RecipientSource?.Type,
                })
                .ToList(),
        };

    private static IReadOnlyList<string> BuildSettingsChangedFields(
        AdManagementSettings beforeEntity,
        UpdateAdManagementSettingsRequest request,
        bool passwordChanged,
        bool notificationRulesChanged)
    {
        var changedFields = new List<string>();

        if (beforeEntity.IsEnabled != request.IsEnabled)
        {
            changedFields.Add("isEnabled");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.DomainFqdn), NormalizeNullable(request.DomainFqdn), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("domainFqdn");
        }

        if (!string.Equals(
                NormalizeNullable(beforeEntity.DefaultUserCreationUpnSuffix),
                NormalizeDefaultUserCreationUpnSuffix(request.DefaultUserCreationUpnSuffix),
                StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("defaultUserCreationUpnSuffix");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.NetbiosDomainName), NormalizeNullable(request.NetbiosDomainName), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("netbiosDomainName");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.DefaultNamingContext), NormalizeNullable(request.DefaultNamingContext), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("defaultNamingContext");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.BaseDn), NormalizeNullable(request.BaseDn), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("baseDn");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.UsersRootOu), NormalizeNullable(request.UsersRootOu), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("usersRootOu");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.DisabledUsersOu), NormalizeNullable(request.DisabledUsersOu), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("disabledUsersOu");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.GroupsSearchBase), NormalizeNullable(request.GroupsSearchBase), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("groupsSearchBase");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.ComputersSearchBase), NormalizeNullable(request.ComputersSearchBase), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("computersSearchBase");
        }

        if (beforeEntity.UseSsl != request.UseSsl)
        {
            changedFields.Add("useSsl");
        }

        if (beforeEntity.LdapPort != request.LdapPort)
        {
            changedFields.Add("ldapPort");
        }

        if (!string.Equals(NormalizeNullable(beforeEntity.ServiceAccountUserName), NormalizeNullable(request.ServiceAccountUserName), StringComparison.OrdinalIgnoreCase))
        {
            changedFields.Add("serviceAccountUserName");
        }

        if (beforeEntity.PowerShellHealthEnabled != request.PowerShellHealthEnabled)
        {
            changedFields.Add("powerShellHealthEnabled");
        }

        if (beforeEntity.PowerShellTimeoutSeconds != request.PowerShellTimeoutSeconds)
        {
            changedFields.Add("powerShellTimeoutSeconds");
        }

        if (passwordChanged)
        {
            changedFields.Add("serviceAccountPassword");
        }

        if (notificationRulesChanged)
        {
            changedFields.Add("notificationSettings");
        }

        return changedFields;
    }

    private static string? NormalizeDefaultUserCreationUpnSuffix(string? value)
    {
        var normalized = NormalizeNullable(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SanitizeValidationMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
    }
}
