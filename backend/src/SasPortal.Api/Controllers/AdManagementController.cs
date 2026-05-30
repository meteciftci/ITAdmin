using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.AdManagement;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/ad-management")]
[Authorize]
public sealed class AdManagementController(
    IAdManagementSettingsService settingsService,
    IAdAttributeMappingService attributeMappingService,
    IAdManagementValidationService validationService,
    IAdUserDirectoryService adUserDirectoryService,
    IAdUserAccountOperationService adUserAccountOperationService,
    IAdUserGroupMembershipService adUserGroupMembershipService) : ControllerBase
{
    [HttpGet("settings")]
    [RequirePermission(AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<AdManagementSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        return Ok(MapSettings(settings));
    }

    [HttpPut("settings")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdManagementSettingsResponse>> UpdateSettings(
        [FromBody] AdManagementSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateSettingsAsync(
            new AppModels.UpdateAdManagementSettingsRequest(
                request.IsEnabled,
                request.DomainFqdn,
                request.DefaultUserCreationUpnSuffix,
                request.NetbiosDomainName,
                request.DefaultNamingContext,
                request.BaseDn,
                request.UsersRootOu,
                request.DisabledUsersOu,
                request.GroupsSearchBase,
                request.ComputersSearchBase,
                request.PreferredDomainControllers,
                request.UseSsl,
                request.LdapPort,
                request.ServiceAccountUserName,
                request.ServiceAccountPassword,
                request.ClearServiceAccountPassword,
                request.PowerShellHealthEnabled,
                request.PowerShellTimeoutSeconds,
                MapNotificationSettingsRequest(request.NotificationSettings),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Settings is null)
        {
            if (result.Validation is not null)
            {
                return BadRequest(new
                {
                    message = result.Message,
                    validation = MapValidation(result.Validation)
                });
            }

            return BadRequest(new { message = result.Message });
        }

        return Ok(MapSettings(result.Settings));
    }

    [HttpPost("settings/validate")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdManagementValidationResponse>> ValidateSettings(
        CancellationToken cancellationToken)
    {
        var validationRequest = new AppModels.AdManagementValidationRequest(
            ResolveActorUserId(User),
            ResolveActorUserName(User),
            ResolveIpAddress(),
            ResolveUserAgent());

        var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);
        var primaryDc = ResolvePrimaryDomainController(connection);

        var result = connection is null
            ? BuildMissingConnectionValidationResult()
            : await validationService.ValidateConnectionAsync(
                connection,
                validationRequest,
                cancellationToken);

        await settingsService.RecordValidationResultAsync(
            result,
            validationRequest,
            primaryDc,
            cancellationToken);

        return Ok(MapValidation(result));
    }

    private static string? ResolvePrimaryDomainController(
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

    private static AppModels.AdManagementValidationResult BuildMissingConnectionValidationResult()
    {
        const string message = "AD yönetim ayarları eksik. Lütfen önce gerekli alanları kaydedin.";
        return new AppModels.AdManagementValidationResult(
            false,
            message,
            DateTimeOffset.UtcNow,
            new List<AppModels.AdManagementValidationDetail>
            {
                new("serviceAccountBind", AdManagementValidationStatuses.Failed, message),
            });
    }

    [HttpGet("users")]
    [RequirePermission(AdManagementPermissions.UsersView)]
    public async Task<ActionResult<AdUserSearchResponse>> SearchUsers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var statusFilter = ParseUserStatusFilter(status);
        var result = await adUserDirectoryService.SearchUsersAsync(
            new AppModels.AdUserSearchQuery(search, statusFilter, pageNumber, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind);
        }

        return Ok(new AdUserSearchResponse(
            result.Page.Items.Select(MapUserListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("upn-suffixes")]
    [RequireAnyPermission(
        AdManagementPermissions.UsersCreate,
        AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<AdUpnSuffixesResponse>> GetUpnSuffixes(
        CancellationToken cancellationToken = default)
    {
        var result = await adUserDirectoryService.GetUpnSuffixesAsync(cancellationToken);
        if (!result.IsSuccess || result.Items is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind);
        }

        return Ok(new AdUpnSuffixesResponse(
            result.Items
                .Select(item => new AdUpnSuffixItemResponse(item.Value, item.Source))
                .ToList(),
            result.Warning));
    }

    [HttpGet("organizational-units")]
    [RequirePermission(AdManagementPermissions.UsersCreate)]
    public async Task<ActionResult<AdOrganizationalUnitSearchResponse>> SearchOrganizationalUnits(
        [FromQuery] string? search,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await adUserDirectoryService.SearchOrganizationalUnitsAsync(
            new AppModels.AdOrganizationalUnitSearchQuery(search, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind);
        }

        return Ok(new AdOrganizationalUnitSearchResponse(
            result.Page.Items
                .Select(item => new AdOrganizationalUnitListItemResponse(
                    item.DistinguishedName,
                    item.Name,
                    item.DisplayName,
                    item.Ou,
                    item.Label))
                .ToList(),
            result.Page.HasMore));
    }

    [HttpPost("users")]
    [RequirePermission(AdManagementPermissions.UsersCreate)]
    public async Task<ActionResult<CreateAdUserResponse>> CreateUser(
        [FromBody] CreateAdUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await adUserDirectoryService.CreateUserAsync(
            new AppModels.CreateAdUserRequest(
                request.GivenName,
                request.Surname,
                request.Department,
                request.SamAccountName,
                request.UpnSuffix,
                request.TargetOuDistinguishedName,
                request.InitialPassword,
                request.IsEnabled,
                request.MustChangePasswordAtNextLogon,
                request.MappedAttributes
                    .Select(item => new AppModels.CreateAdUserMappedAttributeRequest(
                        item.LogicalField,
                        item.Value))
                    .ToList(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.User is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind);
        }

        var user = result.User;
        return Ok(new CreateAdUserResponse(
            user.Id,
            user.DistinguishedName,
            user.Cn,
            user.SamAccountName,
            user.UserPrincipalName,
            user.DisplayName,
            user.IsEnabled,
            user.Message,
            user.NamingCollisionResolved,
            user.GeneratedSuffix,
            user.NotificationSummary is null
                ? null
                : new AdUserCreatedNotificationSummaryResponse(
                    user.NotificationSummary.QueuedCount,
                    user.NotificationSummary.SkippedCount,
                    user.NotificationSummary.Messages)));
    }

    [HttpPost("users/{id}/enable")]
    [RequirePermission(AdManagementPermissions.UsersEnable)]
    public async Task<ActionResult<AdUserAccountOperationResponse>> EnableUser(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteAccountOperationAsync(id, adUserAccountOperationService.EnableAsync, cancellationToken);

    [HttpPost("users/{id}/disable")]
    [RequirePermission(AdManagementPermissions.UsersDisable)]
    public async Task<ActionResult<AdUserAccountOperationResponse>> DisableUser(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteAccountOperationAsync(id, adUserAccountOperationService.DisableAsync, cancellationToken);

    [HttpPost("users/{id}/unlock")]
    [RequirePermission(AdManagementPermissions.UsersUnlock)]
    public async Task<ActionResult<AdUserAccountOperationResponse>> UnlockUser(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteAccountOperationAsync(id, adUserAccountOperationService.UnlockAsync, cancellationToken);

    [HttpGet("users/{id}/groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsView)]
    public async Task<ActionResult<AdUserDirectGroupMembershipsResponse>> GetUserGroups(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = "Geçersiz kullanıcı kimliği." });
        }

        var result = await adUserGroupMembershipService.GetUserGroupsAsync(
            new AppModels.AdUserGroupMembershipRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind);
        }

        return Ok(MapUserGroupMemberships(result));
    }

    [HttpGet("groups/search")]
    [RequirePermission(AdManagementPermissions.UsersGroupsView)]
    public async Task<ActionResult<AdGroupSearchResponse>> SearchGroups(
        [FromQuery] string? query,
        CancellationToken cancellationToken = default)
    {
        var result = await adUserGroupMembershipService.SearchGroupsAsync(
            new AppModels.AdGroupSearchRequest(query),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind);
        }

        return Ok(new AdGroupSearchResponse(
            result.Items?
                .Select(item => new AdGroupSearchItemResponse(
                    item.DistinguishedName,
                    item.DisplayName,
                    item.Name,
                    item.SamAccountName,
                    item.Description))
                .ToList() ?? []));
    }

    [HttpPost("users/{id}/groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsAdd)]
    public async Task<ActionResult<AdUserGroupOperationResponse>> AddUserToGroup(
        [FromRoute] string id,
        [FromBody] AdUserGroupMutationRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteGroupOperationAsync(
            id,
            request.GroupDistinguishedName,
            (userId, groupDn, cancellation) =>
                adUserGroupMembershipService.AddUserToGroupAsync(
                    new AppModels.AddAdUserToGroupRequest(
                        userId,
                        groupDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpDelete("users/{id}/groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsRemove)]
    public async Task<ActionResult<AdUserGroupOperationResponse>> RemoveUserFromGroup(
        [FromRoute] string id,
        [FromBody] AdUserGroupMutationRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteGroupOperationAsync(
            id,
            request.GroupDistinguishedName,
            (userId, groupDn, cancellation) =>
                adUserGroupMembershipService.RemoveUserFromGroupAsync(
                    new AppModels.RemoveAdUserFromGroupRequest(
                        userId,
                        groupDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpPut("users/{id}")]
    [RequirePermission(AdManagementPermissions.UsersUpdate)]
    public async Task<ActionResult<AdUserDetailResponse>> UpdateUser(
        [FromRoute] string id,
        [FromBody] UpdateAdUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = "Geçersiz kullanıcı kimliği." });
        }

        var result = await adUserDirectoryService.UpdateUserAsync(
            new AppModels.UpdateAdUserRequest(
                objectGuid,
                request.GivenName,
                request.Surname,
                request.DisplayName,
                request.SamAccountName,
                request.UserPrincipalName,
                request.Mail,
                request.Department,
                request.MappedAttributes
                    .Select(item => new AppModels.UpdateAdUserMappedAttributeRequest(
                        item.LogicalField,
                        item.Value))
                    .ToList(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.User is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind);
        }

        return Ok(MapUserDetail(result.User));
    }

    [HttpGet("users/{id}")]
    [RequirePermission(AdManagementPermissions.UsersView)]
    public async Task<ActionResult<AdUserDetailResponse>> GetUserById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = "Geçersiz kullanıcı kimliği." });
        }

        var result = await adUserDirectoryService.GetUserByIdAsync(objectGuid, cancellationToken);
        if (!result.IsSuccess || result.User is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind);
        }

        return Ok(MapUserDetail(result.User));
    }

    [HttpGet("attribute-mappings")]
    [RequirePermission(AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<IReadOnlyList<AdAttributeMappingResponse>>> GetMappings(
        CancellationToken cancellationToken)
    {
        var items = await attributeMappingService.GetMappingsAsync(cancellationToken);
        return Ok(items.Select(MapMapping).ToList());
    }

    [HttpPost("attribute-mappings")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdAttributeMappingResponse>> CreateMapping(
        [FromBody] CreateAdAttributeMappingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.CreateAsync(
            new AppModels.CreateAdAttributeMappingRequest(
                request.LogicalField,
                request.DisplayName,
                request.AttributeName,
                request.IsEnabled,
                request.IsEditable,
                request.IsSensitive,
                request.IsSearchable,
                request.ValidationType,
                request.MaskingStrategy,
                request.SortOrder,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Mapping is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapMapping(result.Mapping));
    }

    [HttpPut("attribute-mappings/{id:guid}")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdAttributeMappingResponse>> UpdateMapping(
        [FromRoute] Guid id,
        [FromBody] UpdateAdAttributeMappingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.UpdateAsync(
            new AppModels.UpdateAdAttributeMappingRequest(
                id,
                request.DisplayName,
                request.AttributeName,
                request.IsEnabled,
                request.IsEditable,
                request.IsSensitive,
                request.IsSearchable,
                request.ValidationType,
                request.MaskingStrategy,
                request.SortOrder,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Mapping is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapMapping(result.Mapping));
    }

    [HttpDelete("attribute-mappings/{id:guid}")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult> DeleteMapping(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.DeleteAsync(
            new AppModels.DeleteAdAttributeMappingRequest(
                id,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return NoContent();
    }

    private static AdManagementSettingsResponse MapSettings(AppModels.AdManagementSettingsModel settings) =>
        new(
            settings.IsConfigured,
            settings.IsEnabled,
            settings.DomainFqdn,
            settings.DefaultUserCreationUpnSuffix,
            settings.NetbiosDomainName,
            settings.DefaultNamingContext,
            settings.BaseDn,
            settings.UsersRootOu,
            settings.DisabledUsersOu,
            settings.GroupsSearchBase,
            settings.ComputersSearchBase,
            settings.PreferredDomainControllers,
            settings.UseSsl,
            settings.LdapPort,
            settings.ServiceAccountUserName,
            settings.HasServiceAccountPassword,
            settings.PowerShellHealthEnabled,
            settings.PowerShellTimeoutSeconds,
            settings.LastValidatedAt,
            settings.LastValidationStatus,
            settings.LastValidationMessage,
            MapNotificationSettingsResponse(settings.NotificationSettings));

    private static AdManagementNotificationSettings MapNotificationSettingsRequest(
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

    private static AdManagementNotificationRecipientSource? MapRecipientSourceRequest(
        AdManagementNotificationRecipientSourceRequest? source) =>
        source is null || string.IsNullOrWhiteSpace(source.Type)
            ? null
            : new AdManagementNotificationRecipientSource
            {
                Type = source.Type.Trim(),
                Value = string.IsNullOrWhiteSpace(source.Value) ? null : source.Value.Trim(),
            };

    private static AdManagementNotificationSettingsResponse MapNotificationSettingsResponse(
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

    private static AdManagementNotificationRecipientSourceResponse? MapRecipientSourceResponse(
        AdManagementNotificationRecipientSource? source) =>
        source is null || string.IsNullOrWhiteSpace(source.Type)
            ? null
            : new AdManagementNotificationRecipientSourceResponse
            {
                Type = source.Type,
                Value = source.Value,
            };

    private static AdAttributeMappingResponse MapMapping(AppModels.AdAttributeMappingItem item) =>
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

    private static AdManagementValidationResponse MapValidation(AppModels.AdManagementValidationResult result) =>
        new(
            result.IsValid,
            result.Message,
            result.CheckedAt,
            result.Details
                .Select(d => new AdManagementValidationDetailResponse(d.Key, d.Status, d.Message))
                .ToList());

    private static AppModels.AdUserStatusFilter ParseUserStatusFilter(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "disabled" => AppModels.AdUserStatusFilter.Disabled,
            "all" => AppModels.AdUserStatusFilter.All,
            _ => AppModels.AdUserStatusFilter.Active,
        };

    private async Task<ActionResult<AdUserGroupOperationResponse>> ExecuteGroupOperationAsync(
        string id,
        string groupDistinguishedName,
        Func<Guid, string, CancellationToken, Task<AppModels.AdUserGroupOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdUserGroupOperationResponse(
                false,
                "Geçersiz kullanıcı kimliği.",
                id,
                groupDistinguishedName,
                null));
        }

        if (string.IsNullOrWhiteSpace(groupDistinguishedName))
        {
            return BadRequest(new AdUserGroupOperationResponse(
                false,
                "Grup kimliği zorunludur.",
                id,
                groupDistinguishedName,
                null));
        }

        var result = await operation(
            objectGuid,
            groupDistinguishedName.Trim(),
            cancellationToken);

        var response = new AdUserGroupOperationResponse(
            result.IsSuccess,
            result.Message,
            result.UserId,
            result.GroupDistinguishedName,
            result.GroupName);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            _ => BadRequest(response),
        };
    }

    private static AdUserDirectGroupMembershipsResponse MapUserGroupMemberships(
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

    private async Task<ActionResult<AdUserAccountOperationResponse>> ExecuteAccountOperationAsync(
        string id,
        Func<AppModels.AdUserAccountOperationRequest, CancellationToken, Task<AppModels.AdUserAccountOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdUserAccountOperationResponse(
                false,
                "Geçersiz kullanıcı kimliği.",
                id,
                null,
                null,
                null,
                null,
                null));
        }

        var result = await operation(
            new AppModels.AdUserAccountOperationRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = MapAccountOperationResponse(result);
        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            AppModels.AdDirectoryFailureKind.Disabled
                or AppModels.AdDirectoryFailureKind.NotConfigured
                or AppModels.AdDirectoryFailureKind.MissingPassword => BadRequest(response),
            _ => BadRequest(response),
        };
    }

    private static AdUserAccountOperationResponse MapAccountOperationResponse(
        AppModels.AdUserAccountOperationResult result) =>
        new(
            result.IsSuccess,
            result.Message,
            result.UserId ?? string.Empty,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.IsEnabled,
            result.IsLockedOut);

    private ActionResult MapDirectoryFailure(string message, AppModels.AdDirectoryFailureKind? failureKind) =>
        failureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(new { message }),
            AppModels.AdDirectoryFailureKind.InvalidRequest => BadRequest(new { message }),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message }),
            _ => BadRequest(new { message }),
        };

    private static AdUserListItemResponse MapUserListItem(AppModels.AdUserListItem item) =>
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

    private static AdUserDetailResponse MapUserDetail(AppModels.AdUserDetail item) =>
        new(
            item.Id,
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
            item.Groups
                .Select(MapGroupMembership)
                .ToList(),
            item.MappedAttributes
                .Select(MapMappedAttribute)
                .ToList());

    private static AdUserGroupMembershipResponse MapGroupMembership(AppModels.AdUserGroupMembership item) =>
        new(item.Name, item.DistinguishedName);

    private static MappedAdUserAttributeResponse MapMappedAttribute(AppModels.MappedAdUserAttribute item) =>
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

    private static string? ResolveActorUserName(ClaimsPrincipal principal)
    {
        if (!string.IsNullOrWhiteSpace(principal.Identity?.Name))
        {
            return principal.Identity!.Name;
        }

        var nameClaim = principal.FindFirst(ClaimTypes.Name) ?? principal.FindFirst("name");
        return string.IsNullOrWhiteSpace(nameClaim?.Value) ? null : nameClaim.Value.Trim();
    }

    private static Guid? ResolveActorUserId(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private string? ResolveIpAddress()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? null : ip;
    }

    private string? ResolveUserAgent()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }
}
