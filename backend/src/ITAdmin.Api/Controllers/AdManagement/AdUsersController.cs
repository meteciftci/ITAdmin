using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.AdManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Security;
using AppModels = ITAdmin.Application.Common.Models;
using static ITAdmin.Api.Controllers.AdManagementResponseMappers;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/ad-management")]
[Authorize]
public sealed class AdUsersController(
    IAdUserDirectoryService adUserDirectoryService,
    IAdUserAccountOperationService adUserAccountOperationService,
    IAdUserGroupMembershipService adUserGroupMembershipService,
    IAdUserOuMoveService adUserOuMoveService,
    IAdUserManagerUpdateService adUserManagerUpdateService,
    IAdUserAccountExpirationUpdateService adUserAccountExpirationUpdateService) : AdManagementControllerBase
{

    [HttpGet("users")]
    [RequireAnyPermission(AdManagementPermissions.UsersView, PermissionCodes.Directory.Users.Lookup)]
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
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
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
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
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
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
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
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
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
            result.MessageKey,
            user.NamingCollisionResolved,
            user.GeneratedSuffix,
            user.NotificationSummary is null
                ? null
                : new AdUserCreatedNotificationSummaryResponse(
                    user.NotificationSummary.QueuedCount,
                    user.NotificationSummary.SkippedCount,
                    user.NotificationSummary.Messages),
            result.MessageParams));
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

    [HttpPut("users/{id}/manager")]
    [RequirePermission(AdManagementPermissions.UsersUpdate)]
    public async Task<ActionResult<UpdateAdUserManagerResponse>> UpdateUserManager(
        [FromRoute] string id,
        [FromBody] UpdateAdUserManagerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new UpdateAdUserManagerResponse(
                false,
                AdManagementApiMessageKeys.Users.InvalidUserId,
                id,
                null,
                null,
                null));
        }

        var result = await adUserManagerUpdateService.UpdateManagerAsync(
            new AppModels.UpdateAdUserManagerRequest(
                objectGuid,
                request.ManagerUserId,
                request.ClearManager,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = new UpdateAdUserManagerResponse(
            result.IsSuccess,
            result.MessageKey,
            result.UserId ?? id,
            result.SamAccountName,
            result.ManagerDistinguishedName,
            result.ManagerDisplayName,
            result.MessageParams);

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

    [HttpPut("users/{id}/account-expiration")]
    [RequirePermission(AdManagementPermissions.UsersUpdate)]
    public async Task<ActionResult<UpdateAdUserAccountExpirationResponse>> UpdateUserAccountExpiration(
        [FromRoute] string id,
        [FromBody] UpdateAdUserAccountExpirationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new UpdateAdUserAccountExpirationResponse(
                false,
                AdManagementApiMessageKeys.Users.InvalidUserId,
                id,
                null,
                null,
                request.NeverExpires));
        }

        var result = await adUserAccountExpirationUpdateService.UpdateAccountExpirationAsync(
            new AppModels.UpdateAdUserAccountExpirationRequest(
                objectGuid,
                request.NeverExpires,
                request.ExpiresAt,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = new UpdateAdUserAccountExpirationResponse(
            result.IsSuccess,
            result.MessageKey,
            result.UserId ?? id,
            result.SamAccountName,
            result.AccountExpiresDate,
            result.NeverExpires,
            result.MessageParams);

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

    [HttpPost("users/{id}/move-ou")]
    [RequirePermission(AdManagementPermissions.UsersMoveOu)]
    public async Task<ActionResult<MoveAdUserOuResponse>> MoveUserOu(
        [FromRoute] string id,
        [FromBody] MoveAdUserOuRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new MoveAdUserOuResponse(
                false,
                AdManagementApiMessageKeys.Users.InvalidUserId,
                id,
                null,
                null,
                null,
                null,
                request.TargetOuDistinguishedName));
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            return BadRequest(new MoveAdUserOuResponse(
                false,
                AdManagementApiMessageKeys.Users.TargetOuRequired,
                id,
                null,
                null,
                null,
                null,
                request.TargetOuDistinguishedName));
        }

        var result = await adUserOuMoveService.MoveOuAsync(
            new AppModels.MoveAdUserOuRequest(
                objectGuid,
                request.TargetOuDistinguishedName.Trim(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = new MoveAdUserOuResponse(
            result.IsSuccess,
            result.MessageKey,
            result.UserId ?? id,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.PreviousDistinguishedName,
            result.TargetOuDistinguishedName,
            result.MessageParams);

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

    [HttpGet("users/{id}/groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsView)]
    public async Task<ActionResult<AdUserDirectGroupMembershipsResponse>> GetUserGroups(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Users.InvalidUserId });
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
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapUserGroupMemberships(result));
    }

    [HttpGet("users/{id}/effective-groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsView)]
    public async Task<ActionResult<AdUserEffectiveGroupsResponse>> GetUserEffectiveGroups(
        [FromRoute] string id,
        [FromQuery] int? maxDepth,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Users.InvalidUserId });
        }

        if (maxDepth.HasValue
            && (maxDepth.Value < AdEffectiveGroupMembershipLimits.MinMaxDepth
                || maxDepth.Value > AdEffectiveGroupMembershipLimits.MaxMaxDepth))
        {
            return BadRequest(new
            {
                messageKey = AdManagementApiMessageKeys.Users.EffectiveGroupsMaxDepthOutOfRange,
                messageParams = new Dictionary<string, object>
                {
                    ["min"] = AdEffectiveGroupMembershipLimits.MinMaxDepth,
                    ["max"] = AdEffectiveGroupMembershipLimits.MaxMaxDepth,
                },
            });
        }

        var result = await adUserGroupMembershipService.GetUserEffectiveGroupsAsync(
            new AppModels.AdUserEffectiveGroupsRequest(objectGuid, maxDepth),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapUserEffectiveGroups(result));
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
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
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
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Users.InvalidUserId });
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
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
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
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Users.InvalidUserId });
        }

        var result = await adUserDirectoryService.GetUserByIdAsync(objectGuid, cancellationToken);
        if (!result.IsSuccess || result.User is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapUserDetail(result.User));
    }

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
                AdManagementApiMessageKeys.Users.InvalidUserId,
                id,
                groupDistinguishedName,
                null));
        }

        if (string.IsNullOrWhiteSpace(groupDistinguishedName))
        {
            return BadRequest(new AdUserGroupOperationResponse(
                false,
                AdManagementApiMessageKeys.Groups.GroupDnRequired,
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
            result.MessageKey,
            result.UserId,
            result.GroupDistinguishedName,
            result.GroupName,
            result.MessageParams);

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

    private async Task<ActionResult<AdUserAccountOperationResponse>> ExecuteAccountOperationAsync(
        string id,
        Func<AppModels.AdUserAccountOperationRequest, CancellationToken, Task<AppModels.AdUserAccountOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdUserAccountOperationResponse(
                false,
                AdManagementApiMessageKeys.Users.InvalidUserId,
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
}
