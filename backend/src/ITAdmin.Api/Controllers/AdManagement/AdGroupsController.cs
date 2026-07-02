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
public sealed class AdGroupsController(
    IAdGroupDirectoryService adGroupDirectoryService) : AdManagementControllerBase
{

    [HttpGet("groups")]
    [RequirePermission(AdManagementPermissions.GroupsView)]
    public async Task<ActionResult<AdGroupListResponse>> ListGroups(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await adGroupDirectoryService.SearchGroupsAsync(
            new AppModels.AdGroupListQuery(search, page, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdGroupListResponse(
            result.Page.Items.Select(MapGroupListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("groups/{id}")]
    [RequirePermission(AdManagementPermissions.GroupsView)]
    public async Task<ActionResult<AdGroupDetailResponse>> GetGroupById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var result = await adGroupDirectoryService.GetGroupByIdAsync(objectGuid, cancellationToken);
        if (!result.IsSuccess || result.Group is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapGroupDetail(result.Group));
    }

    [HttpPost("groups")]
    [RequirePermission(AdManagementPermissions.GroupsCreate)]
    public async Task<ActionResult<AdGroupDetailResponse>> CreateGroup(
        [FromBody] CreateAdGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await adGroupDirectoryService.CreateGroupAsync(
            new AppModels.CreateAdGroupRequest(
                request.DisplayName,
                request.Name,
                request.SamAccountName,
                request.Description,
                request.GroupScope,
                request.TargetOuDistinguishedName,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Group is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapGroupDetail(result.Group));
    }

    [HttpPut("groups/{id}")]
    [RequirePermission(AdManagementPermissions.GroupsUpdate)]
    public async Task<ActionResult<AdGroupDetailResponse>> UpdateGroup(
        [FromRoute] string id,
        [FromBody] UpdateAdGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var result = await adGroupDirectoryService.UpdateGroupAsync(
            new AppModels.UpdateAdGroupRequest(
                objectGuid,
                request.DisplayName,
                request.Name,
                request.SamAccountName,
                request.Description,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Group is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapGroupDetail(result.Group));
    }

    [HttpDelete("groups/{id}")]
    [RequirePermission(AdManagementPermissions.GroupsDelete)]
    public async Task<ActionResult<DeleteAdGroupResponse>> DeleteGroup(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var result = await adGroupDirectoryService.DeleteGroupAsync(
            new AppModels.DeleteAdGroupRequest(
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

        return Ok(new DeleteAdGroupResponse(
            true,
            result.MessageKey,
            result.DeletedGroupId,
            result.MessageParams));
    }

    [HttpPost("groups/{id}/move-ou")]
    [RequirePermission(AdManagementPermissions.GroupsMoveOu)]
    public async Task<ActionResult<MoveAdGroupOuResponse>> MoveGroupOu(
        [FromRoute] string id,
        [FromBody] MoveAdGroupOuRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new MoveAdGroupOuResponse(
                false,
                AdManagementApiMessageKeys.Groups.InvalidGroupId,
                id,
                null,
                null,
                null,
                null,
                null,
                request.TargetOuDistinguishedName));
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            return BadRequest(new MoveAdGroupOuResponse(
                false,
                AdManagementApiMessageKeys.Groups.TargetOuRequired,
                id,
                null,
                null,
                null,
                null,
                null,
                request.TargetOuDistinguishedName));
        }

        var result = await adGroupDirectoryService.MoveGroupOuAsync(
            new AppModels.MoveAdGroupOuRequest(
                objectGuid,
                request.TargetOuDistinguishedName.Trim(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = new MoveAdGroupOuResponse(
            result.IsSuccess,
            result.MessageKey,
            result.GroupId ?? id,
            result.DisplayName,
            result.Name,
            result.SamAccountName,
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

    [HttpGet("groups/{id}/members")]
    [RequirePermission(AdManagementPermissions.GroupsView)]
    public async Task<ActionResult<AdGroupMembersListResponse>> GetGroupMembers(
        [FromRoute] string id,
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var result = await adGroupDirectoryService.GetGroupMembersAsync(
            new AppModels.AdGroupMembersListQuery(objectGuid, search, type, pageNumber, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdGroupMembersListResponse(
            result.Page.Items.Select(MapGroupMemberListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.MemberCount,
            result.Page.HasNextPage));
    }

    [HttpGet("groups/{id}/member-candidates")]
    [RequirePermission(AdManagementPermissions.GroupsManageMembers)]
    public async Task<ActionResult<AdGroupMemberCandidatesResponse>> SearchGroupMemberCandidates(
        [FromRoute] string id,
        [FromQuery] string? search,
        [FromQuery] string? types,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var typeList = string.IsNullOrWhiteSpace(types)
            ? Array.Empty<string>()
            : types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = await adGroupDirectoryService.SearchGroupMemberCandidatesAsync(
            new AppModels.AdGroupMemberCandidatesQuery(objectGuid, search, typeList, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Items is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdGroupMemberCandidatesResponse(
            result.Items.Select(MapGroupMemberCandidateItem).ToList()));
    }

    [HttpPost("groups/{id}/members")]
    [RequirePermission(AdManagementPermissions.GroupsManageMembers)]
    public async Task<ActionResult<AdGroupMemberOperationResponse>> AddGroupMember(
        [FromRoute] string id,
        [FromBody] AddAdGroupMemberRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteGroupMemberOperationAsync(
            id,
            request.MemberDistinguishedName,
            request.MemberType,
            (groupId, memberDn, memberType, cancellation) =>
                adGroupDirectoryService.AddGroupMemberAsync(
                    new AppModels.AddAdGroupMemberRequest(
                        groupId,
                        memberDn,
                        memberType,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpDelete("groups/{id}/members")]
    [RequirePermission(AdManagementPermissions.GroupsManageMembers)]
    public async Task<ActionResult<AdGroupMemberOperationResponse>> RemoveGroupMember(
        [FromRoute] string id,
        [FromBody] RemoveAdGroupMemberRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteGroupMemberOperationAsync(
            id,
            request.MemberDistinguishedName,
            memberType: null,
            (groupId, memberDn, _, cancellation) =>
                adGroupDirectoryService.RemoveGroupMemberAsync(
                    new AppModels.RemoveAdGroupMemberRequest(
                        groupId,
                        memberDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpGet("group-organizational-units")]
    [RequireAnyPermission(
        AdManagementPermissions.GroupsCreate,
        AdManagementPermissions.GroupsMoveOu)]
    public async Task<ActionResult<AdOrganizationalUnitSearchResponse>> SearchGroupOrganizationalUnits(
        [FromQuery] string? search,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await adGroupDirectoryService.SearchGroupOrganizationalUnitsAsync(
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

    private async Task<ActionResult<AdGroupMemberOperationResponse>> ExecuteGroupMemberOperationAsync(
        string id,
        string memberDistinguishedName,
        string? memberType,
        Func<Guid, string, string?, CancellationToken, Task<AppModels.AdGroupMemberOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdGroupMemberOperationResponse(
                false,
                AdManagementApiMessageKeys.Groups.InvalidGroupId,
                id,
                null,
                null,
                memberDistinguishedName,
                null));
        }

        if (string.IsNullOrWhiteSpace(memberDistinguishedName))
        {
            return BadRequest(new AdGroupMemberOperationResponse(
                false,
                AdManagementApiMessageKeys.Groups.MemberOperationFailed,
                id,
                null,
                null,
                memberDistinguishedName,
                null));
        }

        var result = await operation(
            objectGuid,
            memberDistinguishedName.Trim(),
            memberType,
            cancellationToken);

        var response = new AdGroupMemberOperationResponse(
            result.IsSuccess,
            result.MessageKey,
            result.GroupId,
            result.GroupDistinguishedName,
            result.GroupName,
            result.MemberDistinguishedName,
            result.MemberName,
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
}
