using System.Security.Claims;
using ITAdmin.Api.Contracts.AdManagement;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Security;
using AppModels = ITAdmin.Application.Common.Models;

namespace ITAdmin.Api.Controllers;

internal static class AdManagementResponseMappers
{
    internal static string? ResolvePrimaryDomainController(
        AppModels.AdManagementConnectionParameters? connection)
    {
        if (connection is null)
        {
            return null;
        }

        if (connection.PreferredDomainControllers.Count > 0)
        {
            var first = connection.PreferredDomainControllers[0];
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return string.IsNullOrWhiteSpace(connection.DomainFqdn) ? null : connection.DomainFqdn;
    }

    internal static AppModels.AdManagementValidationResult BuildMissingConnectionValidationResult()
    {
        var messageKey = AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings;
        return new AppModels.AdManagementValidationResult(
            false,
            messageKey,
            DateTimeOffset.UtcNow,
            new List<AppModels.AdManagementValidationDetail>
            {
                new("serviceAccountBind", AdManagementValidationStatuses.Failed, messageKey),
            });
    }

    internal static AdManagementSettingsResponse MapSettings(AppModels.AdManagementSettingsModel settings) =>
        new(
            settings.IsConfigured,
            settings.IsEnabled,
            settings.DomainFqdn,
            settings.DefaultUserCreationUpnSuffix,
            settings.DefaultUserOu,
            settings.DefaultGroupOu,
            settings.DefaultComputerOu,
            settings.NetbiosDomainName,
            settings.DefaultNamingContext,
            settings.BaseDn,
            settings.UsersRootOu,
            settings.DisabledUsersOu,
            settings.GroupsSearchBase,
            settings.ComputersSearchBase,
            settings.PreferredDomainControllers,
            settings.ServiceAccountUserName,
            settings.HasServiceAccountPassword,
            settings.PowerShellHealthEnabled,
            settings.PowerShellTimeoutSeconds,
            settings.LastValidatedAt,
            settings.LastValidationStatus,
            settings.LastValidationMessage,
            MapNotificationSettingsResponse(settings.NotificationSettings));

    internal static AdManagementNotificationSettings MapNotificationSettingsRequest(
        AdManagementNotificationSettingsRequest? request)
    {
        if (request is null)
        {
            return AdManagementNotificationSettingsSerializer.CreateDefault();
        }

        return new AdManagementNotificationSettings
        {
            Rules = request.Rules
                .Select(rule => new AdManagementNotificationRule
                {
                    Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id,
                    EventKey = rule.EventKey.Trim(),
                    Channel = rule.Channel.Trim(),
                    IsEnabled = rule.IsEnabled,
                    RecipientSource = MapRecipientSourceRequest(rule.RecipientSource),
                })
                .ToList(),
        };
    }

    internal static AdManagementNotificationRecipientSource? MapRecipientSourceRequest(
        AdManagementNotificationRecipientSourceRequest? source) =>
        source is null || string.IsNullOrWhiteSpace(source.Type)
            ? null
            : new AdManagementNotificationRecipientSource
            {
                Type = source.Type.Trim(),
                Value = string.IsNullOrWhiteSpace(source.Value) ? null : source.Value.Trim(),
            };

    internal static AdManagementNotificationSettingsResponse MapNotificationSettingsResponse(
        AdManagementNotificationSettings settings) =>
        new()
        {
            Rules = settings.Rules
                .Select(rule => new AdManagementNotificationRuleResponse
                {
                    Id = rule.Id,
                    EventKey = rule.EventKey,
                    Channel = rule.Channel,
                    IsEnabled = rule.IsEnabled,
                    RecipientSource = MapRecipientSourceResponse(rule.RecipientSource),
                })
                .ToList(),
        };

    internal static AdManagementNotificationRecipientSourceResponse? MapRecipientSourceResponse(
        AdManagementNotificationRecipientSource? source) =>
        source is null || string.IsNullOrWhiteSpace(source.Type)
            ? null
            : new AdManagementNotificationRecipientSourceResponse
            {
                Type = source.Type,
                Value = source.Value,
            };

    internal static AdAttributeMappingResponse MapMapping(AppModels.AdAttributeMappingItem item) =>
        new(
            item.Id,
            item.LogicalField,
            item.DisplayName,
            item.AttributeName,
            item.IsEnabled,
            item.IsEditable,
            item.IsSensitive,
            item.IsSearchable,
            item.ValidationType,
            item.MaskingStrategy,
            item.SortOrder);

    internal static AdManagementValidationResponse MapValidation(
        AppModels.AdManagementValidationResult result,
        AppModels.AdDeletedObjectRestoreReadinessResult? restoreReadiness = null) =>
        new(
            result.IsValid,
            result.MessageKey,
            result.CheckedAt,
            result.Details
                .Select(d => new AdManagementValidationDetailResponse(d.Key, d.Status, d.MessageKey, d.MessageParams))
                .ToList(),
            restoreReadiness is null ? null : MapRestoreReadiness(restoreReadiness),
            result.MessageParams);

    internal static AdDeletedObjectRestoreReadinessResponse MapRestoreReadiness(
        AppModels.AdDeletedObjectRestoreReadinessResult result) =>
        new(
            result.IsReady,
            result.Status,
            result.SummaryMessage,
            result.BlockingReasons.Select(MapRestoreReadinessCheck).ToList(),
            result.Warnings.Select(MapRestoreReadinessCheck).ToList(),
            result.Checks.Select(MapRestoreReadinessCheck).ToList(),
            result.CheckedAtUtc,
            result.DomainController,
            result.SummaryKey,
            result.SummaryParams);

    internal static AdDeletedObjectRestoreReadinessCheckResponse MapRestoreReadinessCheck(
        AppModels.AdDeletedObjectRestoreReadinessCheck check) =>
        new(
            check.Key,
            check.Status,
            check.Title,
            check.Remediation,
            check.Command,
            check.IsBlocking,
            check.Details,
            check.TitleKey,
            check.TitleParams,
            check.MessageKey,
            check.MessageParams,
            check.RemediationKey,
            check.RemediationParams);

    internal static AppModels.AdUserStatusFilter ParseUserStatusFilter(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "disabled" => AppModels.AdUserStatusFilter.Disabled,
            "all" => AppModels.AdUserStatusFilter.All,
            _ => AppModels.AdUserStatusFilter.Active,
        };

    internal static AdComputerDirectGroupMembershipsResponse MapComputerGroupMemberships(
        AppModels.AdComputerGroupMembershipResult result) =>
        new(
            result.ComputerId ?? string.Empty,
            result.Name,
            result.SamAccountName,
            result.DnsHostName,
            result.DistinguishedName,
            result.Groups?
                .Select(group => new AdComputerGroupMembershipItemResponse(
                    group.Id,
                    group.DistinguishedName,
                    group.DisplayName,
                    group.Name,
                    group.SamAccountName,
                    group.Description,
                    group.IsDirect))
                .ToList() ?? []);

    internal static AdUserDirectGroupMembershipsResponse MapUserGroupMemberships(
        AppModels.AdUserGroupMembershipResult result) =>
        new(
            result.UserId ?? string.Empty,
            result.DisplayName,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.Groups?
                .Select(group => new AdUserGroupMembershipItemResponse(
                    group.DistinguishedName,
                    group.DisplayName,
                    group.Name,
                    group.SamAccountName,
                    group.Description,
                    group.IsDirect))
                .ToList() ?? []);

    internal static AdUserEffectiveGroupsResponse MapUserEffectiveGroups(
        AppModels.AdUserEffectiveGroupsResult result) =>
        new(
            result.UserId ?? string.Empty,
            result.DisplayName,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.DirectGroups?
                .Select(group => new AdEffectiveGroupSummaryItemResponse(
                    group.Name,
                    group.DistinguishedName,
                    group.SamAccountName,
                    group.Description,
                    group.DisplayName))
                .ToList() ?? [],
            result.EffectiveGroups?
                .Select(group => new AdEffectiveGroupNestedItemResponse(
                    group.Name,
                    group.DistinguishedName,
                    group.SamAccountName,
                    group.Description,
                    group.DisplayName,
                    group.Depth,
                    group.IsDirect,
                    group.Path
                        .Select(node => new AdMembershipPathNodeResponse(
                            node.Type,
                            node.Name,
                            node.DisplayName,
                            node.SamAccountName,
                            node.DistinguishedName))
                        .ToList()))
                .ToList() ?? [],
            result.MaxDepth,
            result.Truncated,
            result.TruncatedReason);

    internal static AdComputerAccountOperationResponse MapComputerAccountOperationResponse(
        AppModels.AdComputerAccountOperationResult result) =>
        new(
            result.IsSuccess,
            result.MessageKey,
            result.Computer is null ? null : MapComputerDetail(result.Computer),
            result.MessageParams);

    internal static AdUserAccountOperationResponse MapAccountOperationResponse(
        AppModels.AdUserAccountOperationResult result) =>
        new(
            result.IsSuccess,
            result.MessageKey,
            result.UserId ?? string.Empty,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.IsEnabled,
            result.IsLockedOut,
            result.MessageParams);

    internal static AdUserListItemResponse MapUserListItem(AppModels.AdUserListItem item) =>
        new(
            item.Id,
            item.DistinguishedName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DisplayName,
            item.Mail,
            item.Department,
            item.IsEnabled,
            item.IsLockedOut,
            item.WhenCreated,
            item.WhenChanged,
            item.LastLogonAt);

    internal static AdOrganizationalUnitManageListItemResponse MapOrganizationalUnitManageListItem(
        AppModels.AdOrganizationalUnitManageListItem item) =>
        new(
            item.ObjectGuid,
            item.Name,
            item.Ou,
            item.DisplayName,
            item.DisplayLabel,
            item.DistinguishedName,
            item.ParentDistinguishedName,
            item.CanonicalName,
            item.ChildOuCount,
            item.UserCount,
            item.GroupCount,
            item.ComputerCount);

    internal static AdOrganizationalUnitDetailResponse MapOrganizationalUnitDetail(
        AppModels.AdOrganizationalUnitDetail item) =>
        new(
            item.ObjectGuid,
            item.Name,
            item.Ou,
            item.DisplayName,
            item.DistinguishedName,
            item.ParentDistinguishedName,
            item.CanonicalName,
            new AdOrganizationalUnitContentSummaryResponse(
                item.ContentSummary.ChildOuCount,
                item.ContentSummary.UserCount,
                item.ContentSummary.GroupCount,
                item.ContentSummary.ComputerCount),
            item.ChildOrganizationalUnits
                .Select(child => new AdOrganizationalUnitChildListItemResponse(
                    child.ObjectGuid,
                    child.Name,
                    child.Ou,
                    child.DisplayName,
                    child.DisplayLabel,
                    child.DistinguishedName,
                    child.CanonicalName))
                .ToList());

    internal static AdComputerListItemResponse MapComputerListItem(AppModels.AdComputerListItem item) =>
        new(
            item.Id,
            item.Name,
            item.SamAccountName,
            item.DnsHostName,
            item.OperatingSystem,
            item.DistinguishedName,
            item.IsEnabled,
            item.WhenChanged);

    internal static AdComputerDetailResponse MapComputerDetail(AppModels.AdComputerDetail item) =>
        new(
            item.Id,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.DnsHostName,
            item.DistinguishedName,
            item.ParentOuDistinguishedName,
            item.Description,
            item.OperatingSystem,
            item.OperatingSystemVersion,
            item.OperatingSystemServicePack,
            item.ManagedByDistinguishedName,
            item.ManagedByDisplayName,
            item.LastLogonAt,
            item.WhenCreated,
            item.WhenChanged,
            item.UserAccountControl,
            item.IsEnabled,
            item.PrimaryGroupId,
            item.MemberOfCount,
            item.MemberOf.Select(MapComputerMemberOfItem).ToList(),
            item.MemberOfTruncated);

    internal static AdComputerMemberOfItemResponse MapComputerMemberOfItem(
        AppModels.AdComputerMemberOfItem item) =>
        new(item.DistinguishedName, item.Name, item.SamAccountName);

    internal static AdDeletedObjectListItemResponse MapDeletedObjectListItem(
        AppModels.AdDeletedObjectListItem item) =>
        new(
            item.Id,
            item.ObjectType.ToString(),
            item.Name,
            item.DisplayName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DistinguishedName,
            item.LastKnownParent,
            item.WhenChanged,
            item.DeletedAt);

    internal static AdDeletedObjectDetailResponse MapDeletedObjectDetail(
        AppModels.AdDeletedObjectDetail item) =>
        new(
            item.Id,
            item.ObjectType.ToString(),
            item.Name,
            item.DisplayName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.Description,
            item.DistinguishedName,
            item.LastKnownParent,
            item.LastKnownRdn,
            item.ObjectClass,
            item.ObjectSid,
            item.WhenCreated,
            item.WhenChanged,
            item.DeletedAt,
            item.Mail,
            item.Department,
            item.DnsHostName,
            item.OperatingSystem,
            item.MemberOfCount,
            item.MemberOf,
            item.MemberOfTruncated,
            item.AdditionalAttributes);

    internal static AdGroupListItemResponse MapGroupListItem(AppModels.AdGroupListItem item) =>
        new(
            item.Id,
            item.DistinguishedName,
            item.DisplayName,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.Description,
            item.GroupScope,
            item.SecurityEnabled,
            item.GroupType);

    internal static AdGroupDetailResponse MapGroupDetail(AppModels.AdGroupDetail item) =>
        new(
            item.Id,
            item.DistinguishedName,
            item.DisplayName,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.Description,
            item.GroupScope,
            item.SecurityEnabled,
            item.GroupType,
            item.WhenCreated,
            item.WhenChanged,
            item.ManagedByDistinguishedName,
            item.ManagedByDisplayName,
            item.MemberCount,
            item.MemberOfCount,
            item.Members.Select(MapGroupMemberItem).ToList(),
            item.MemberOf.Select(MapGroupMemberItem).ToList(),
            item.MembersTruncated,
            item.MemberOfTruncated);

    internal static AdGroupMemberItemResponse MapGroupMemberItem(AppModels.AdGroupMemberItem item) =>
        new(
            item.Type,
            item.DisplayName,
            item.Name,
            item.SamAccountName,
            item.DistinguishedName,
            item.Description);

    internal static AdGroupMemberListItemResponse MapGroupMemberListItem(AppModels.AdGroupMemberListItem item) =>
        new(
            item.Id,
            item.Type,
            item.DisplayName,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DNSHostName,
            item.Description,
            item.DistinguishedName,
            item.IsDirectMember);

    internal static AdGroupMemberCandidateItemResponse MapGroupMemberCandidateItem(
        AppModels.AdGroupMemberCandidateItem item) =>
        new(
            item.Id,
            item.Type,
            item.DisplayName,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DNSHostName,
            item.Description,
            item.DistinguishedName,
            item.IsAlreadyDirectMember,
            item.IsEnabled);

    internal static AdUserDetailResponse MapUserDetail(AppModels.AdUserDetail item) =>
        new(
            item.Id,
            item.DistinguishedName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DisplayName,
            item.Mail,
            item.GivenName,
            item.Surname,
            item.Department,
            item.IsEnabled,
            item.IsLockedOut,
            item.PasswordLastSetAt,
            item.LastLogonAt,
            item.WhenCreated,
            item.WhenChanged,
            item.UserAccountControl,
            item.AccountExpiresAt,
            item.AccountExpiresDate,
            item.LockoutTimeAt,
            item.BadPwdCount,
            item.BadPasswordTimeAt,
            item.LastLogonTimestampAt,
            item.Groups
                .Select(MapGroupMembership)
                .ToList(),
            item.MappedAttributes
                .Select(MapMappedAttribute)
                .ToList(),
            item.ManagerDistinguishedName,
            item.ManagerId,
            item.ManagerSamAccountName,
            item.ManagerUserPrincipalName,
            item.ManagerDisplayName);

    internal static AdUserGroupMembershipResponse MapGroupMembership(AppModels.AdUserGroupMembership item) =>
        new(item.Name, item.DistinguishedName);

    internal static MappedAdUserAttributeResponse MapMappedAttribute(AppModels.MappedAdUserAttribute item) =>
        new(
            item.LogicalField,
            item.DisplayName,
            item.AdAttribute,
            item.Value,
            item.IsSensitive,
            item.MaskingStrategy,
            item.IsEditable,
            item.IsSearchable,
            item.SortOrder);

    internal static string? ResolveActorUserName(ClaimsPrincipal principal)
    {
        if (!string.IsNullOrWhiteSpace(principal.Identity?.Name))
        {
            return principal.Identity!.Name;
        }

        var nameClaim = principal.FindFirst(ClaimTypes.Name) ?? principal.FindFirst("name");
        return string.IsNullOrWhiteSpace(nameClaim?.Value) ? null : nameClaim.Value.Trim();
    }

    internal static Guid? ResolveActorUserId(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
