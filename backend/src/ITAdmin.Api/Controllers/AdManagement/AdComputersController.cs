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
public sealed class AdComputersController(
    IAdComputerDirectoryService adComputerDirectoryService,
    IAdComputerAccountOperationService adComputerAccountOperationService,
    IAdComputerUpdateService adComputerUpdateService,
    IAdComputerOuMoveService adComputerOuMoveService,
    IAdComputerDeleteService adComputerDeleteService,
    IAdComputerGroupMembershipService adComputerGroupMembershipService) : AdManagementControllerBase
{

    [HttpGet("computers")]
    [RequirePermission(AdManagementPermissions.ComputersView)]
    public async Task<ActionResult<AdComputerListResponse>> ListComputers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? operatingSystem,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var statusFilter = ParseUserStatusFilter(status);
        var result = await adComputerDirectoryService.SearchComputersAsync(
            new AppModels.AdComputerListQuery(
                search,
                statusFilter,
                string.IsNullOrWhiteSpace(operatingSystem) ? null : operatingSystem.Trim(),
                pageNumber,
                pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdComputerListResponse(
            result.Page.Items.Select(MapComputerListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("computers/{id}")]
    [RequirePermission(AdManagementPermissions.ComputersView)]
    public async Task<ActionResult<AdComputerDetailResponse>> GetComputerById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Computers.InvalidComputerId });
        }

        var result = await adComputerDirectoryService.GetComputerByIdAsync(objectGuid, cancellationToken);
        if (!result.IsSuccess || result.Computer is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapComputerDetail(result.Computer));
    }

    [HttpGet("computer-operating-systems")]
    [RequirePermission(AdManagementPermissions.ComputersView)]
    public async Task<ActionResult<AdComputerOperatingSystemOptionsResponse>> GetComputerOperatingSystems(
        CancellationToken cancellationToken = default)
    {
        var result = await adComputerDirectoryService.GetComputerOperatingSystemsAsync(cancellationToken);
        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdComputerOperatingSystemOptionsResponse(result.Page.Items));
    }

    [HttpPost("computers/{id}/enable")]
    [RequirePermission(AdManagementPermissions.ComputersEnable)]
    public async Task<ActionResult<AdComputerAccountOperationResponse>> EnableComputer(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteComputerAccountOperationAsync(
            id,
            adComputerAccountOperationService.EnableComputerAsync,
            cancellationToken);

    [HttpPost("computers/{id}/disable")]
    [RequirePermission(AdManagementPermissions.ComputersDisable)]
    public async Task<ActionResult<AdComputerAccountOperationResponse>> DisableComputer(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteComputerAccountOperationAsync(
            id,
            adComputerAccountOperationService.DisableComputerAsync,
            cancellationToken);

    [HttpPut("computers/{id}")]
    [RequirePermission(AdManagementPermissions.ComputersUpdate)]
    public async Task<ActionResult<AdComputerAccountOperationResponse>> UpdateComputer(
        [FromRoute] string id,
        [FromBody] UpdateAdComputerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdComputerAccountOperationResponse(
                false,
                AdManagementApiMessageKeys.Computers.InvalidComputerId,
                null));
        }

        var result = await adComputerUpdateService.UpdateComputerAsync(
            new AppModels.UpdateAdComputerRequest(
                objectGuid,
                request.Description,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        return MapComputerOperationActionResult(result.IsSuccess, result.MessageKey, result.Computer, result.FailureKind, result.MessageParams);
    }

    [HttpPost("computers/{id}/move-ou")]
    [RequirePermission(AdManagementPermissions.ComputersMoveOu)]
    public async Task<ActionResult<AdComputerAccountOperationResponse>> MoveComputerOu(
        [FromRoute] string id,
        [FromBody] MoveAdComputerOuRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdComputerAccountOperationResponse(
                false,
                AdManagementApiMessageKeys.Computers.InvalidComputerId,
                null));
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            return BadRequest(new AdComputerAccountOperationResponse(
                false,
                AdManagementApiMessageKeys.Computers.TargetOuRequired,
                null));
        }

        var result = await adComputerOuMoveService.MoveOuAsync(
            new AppModels.MoveAdComputerOuRequest(
                objectGuid,
                request.TargetOuDistinguishedName.Trim(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        return MapComputerOperationActionResult(result.IsSuccess, result.MessageKey, result.Computer, result.FailureKind, result.MessageParams);
    }

    [HttpGet("computers/{id}/groups")]
    [RequirePermission(AdManagementPermissions.ComputersGroupsView)]
    public async Task<ActionResult<AdComputerDirectGroupMembershipsResponse>> GetComputerGroups(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Computers.InvalidComputerId });
        }

        var result = await adComputerGroupMembershipService.GetComputerGroupsAsync(
            new AppModels.AdComputerGroupMembershipRequest(
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

        return Ok(MapComputerGroupMemberships(result));
    }

    [HttpGet("computers/{id}/group-candidates")]
    [RequirePermission(AdManagementPermissions.ComputersGroupsAdd)]
    public async Task<ActionResult<AdComputerGroupCandidateSearchResponse>> SearchComputerGroupCandidates(
        [FromRoute] string id,
        [FromQuery] string? query,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Computers.InvalidComputerId });
        }

        var result = await adComputerGroupMembershipService.SearchGroupCandidatesAsync(
            new AppModels.AdComputerGroupSearchRequest(objectGuid, query),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdComputerGroupCandidateSearchResponse(
            result.Items?
                .Select(item => new AdComputerGroupCandidateItemResponse(
                    item.DistinguishedName,
                    item.DisplayName,
                    item.Name,
                    item.SamAccountName,
                    item.Description))
                .ToList() ?? []));
    }

    [HttpPost("computers/{id}/groups")]
    [RequirePermission(AdManagementPermissions.ComputersGroupsAdd)]
    public async Task<ActionResult<AdComputerGroupOperationResponse>> AddComputerToGroup(
        [FromRoute] string id,
        [FromBody] AdComputerGroupMutationRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteComputerGroupOperationAsync(
            id,
            request.GroupDistinguishedName,
            (computerId, groupDn, cancellation) =>
                adComputerGroupMembershipService.AddComputerToGroupAsync(
                    new AppModels.AddAdComputerToGroupRequest(
                        computerId,
                        groupDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpDelete("computers/{id}/groups")]
    [RequirePermission(AdManagementPermissions.ComputersGroupsRemove)]
    public async Task<ActionResult<AdComputerGroupOperationResponse>> RemoveComputerFromGroup(
        [FromRoute] string id,
        [FromBody] AdComputerGroupMutationRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteComputerGroupOperationAsync(
            id,
            request.GroupDistinguishedName,
            (computerId, groupDn, cancellation) =>
                adComputerGroupMembershipService.RemoveComputerFromGroupAsync(
                    new AppModels.RemoveAdComputerFromGroupRequest(
                        computerId,
                        groupDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpDelete("computers/{id}")]
    [RequirePermission(AdManagementPermissions.ComputersDelete)]
    public async Task<ActionResult<DeleteAdComputerResponse>> DeleteComputer(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new DeleteAdComputerResponse(
                false,
                AdManagementApiMessageKeys.Computers.InvalidComputerId,
                null,
                null,
                null));
        }

        var result = await adComputerDeleteService.DeleteComputerAsync(
            new AppModels.DeleteAdComputerRequest(
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

        return Ok(new DeleteAdComputerResponse(
            true,
            result.MessageKey,
            result.DeletedComputerId,
            result.DeletedComputerName,
            result.DeletedDistinguishedName,
            result.MessageParams));
    }

    [HttpGet("computer-organizational-units")]
    [RequirePermission(AdManagementPermissions.ComputersView)]
    public async Task<ActionResult<AdOrganizationalUnitSearchResponse>> SearchComputerOrganizationalUnits(
        [FromQuery] string? search,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await adComputerDirectoryService.SearchComputerOrganizationalUnitsAsync(
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

    private async Task<ActionResult<AdComputerGroupOperationResponse>> ExecuteComputerGroupOperationAsync(
        string id,
        string groupDistinguishedName,
        Func<Guid, string, CancellationToken, Task<AppModels.AdComputerGroupOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdComputerGroupOperationResponse(
                false,
                AdManagementApiMessageKeys.Computers.InvalidComputerId,
                id,
                null,
                null,
                groupDistinguishedName,
                null,
                null,
                null));
        }

        if (string.IsNullOrWhiteSpace(groupDistinguishedName))
        {
            return BadRequest(new AdComputerGroupOperationResponse(
                false,
                AdManagementApiMessageKeys.Groups.GroupDnRequired,
                id,
                null,
                null,
                groupDistinguishedName,
                null,
                null,
                null));
        }

        var result = await operation(
            objectGuid,
            groupDistinguishedName.Trim(),
            cancellationToken);

        var response = new AdComputerGroupOperationResponse(
            result.IsSuccess,
            result.MessageKey,
            result.ComputerId,
            result.ComputerName,
            result.ComputerSamAccountName,
            result.GroupDistinguishedName,
            result.GroupName,
            result.GroupDisplayName,
            result.GroupSamAccountName,
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

    private async Task<ActionResult<AdComputerAccountOperationResponse>> ExecuteComputerAccountOperationAsync(
        string id,
        Func<AppModels.AdComputerAccountOperationRequest, CancellationToken, Task<AppModels.AdComputerAccountOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdComputerAccountOperationResponse(
                false,
                AdManagementApiMessageKeys.Computers.InvalidComputerId,
                null));
        }

        var result = await operation(
            new AppModels.AdComputerAccountOperationRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = MapComputerAccountOperationResponse(result);
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
                or AppModels.AdDirectoryFailureKind.MissingPassword
                or AppModels.AdDirectoryFailureKind.InvalidRequest => BadRequest(response),
            _ => BadRequest(response),
        };
    }

    private ActionResult<AdComputerAccountOperationResponse> MapComputerOperationActionResult(
        bool isSuccess,
        string messageKey,
        AppModels.AdComputerDetail? computer,
        AppModels.AdDirectoryFailureKind? failureKind,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        var response = new AdComputerAccountOperationResponse(
            isSuccess,
            messageKey,
            computer is null ? null : MapComputerDetail(computer),
            messageParams);

        if (isSuccess)
        {
            return Ok(response);
        }

        return failureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            AppModels.AdDirectoryFailureKind.Disabled
                or AppModels.AdDirectoryFailureKind.NotConfigured
                or AppModels.AdDirectoryFailureKind.MissingPassword
                or AppModels.AdDirectoryFailureKind.InvalidRequest => BadRequest(response),
            _ => BadRequest(response),
        };
    }
}
