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
public sealed class AdOrganizationalUnitsController(
    IAdOrganizationalUnitDirectoryService adOrganizationalUnitDirectoryService) : AdManagementControllerBase
{

    [HttpGet("settings/organizational-units")]
    [RequirePermission(AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<AdOrganizationalUnitManageListResponse>> ListSettingsOrganizationalUnits(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await adOrganizationalUnitDirectoryService.SearchManageOrganizationalUnitsAsync(
            new AppModels.AdOrganizationalUnitManageListQuery(search, pageNumber, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdOrganizationalUnitManageListResponse(
            result.Page.Items.Select(MapOrganizationalUnitManageListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("organizational-units/manage")]
    [RequirePermission(AdManagementPermissions.OrganizationalUnitsView)]
    public async Task<ActionResult<AdOrganizationalUnitManageListResponse>> ListManageOrganizationalUnits(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await adOrganizationalUnitDirectoryService.SearchManageOrganizationalUnitsAsync(
            new AppModels.AdOrganizationalUnitManageListQuery(search, pageNumber, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdOrganizationalUnitManageListResponse(
            result.Page.Items.Select(MapOrganizationalUnitManageListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("organizational-units/{id}")]
    [RequirePermission(AdManagementPermissions.OrganizationalUnitsView)]
    public async Task<ActionResult<AdOrganizationalUnitDetailResponse>> GetOrganizationalUnitById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.OrganizationalUnits.InvalidOrganizationalUnitId });
        }

        var result = await adOrganizationalUnitDirectoryService.GetOrganizationalUnitByIdAsync(
            objectGuid,
            cancellationToken);
        if (!result.IsSuccess || result.OrganizationalUnit is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapOrganizationalUnitDetail(result.OrganizationalUnit));
    }

    [HttpPost("organizational-units")]
    [RequirePermission(AdManagementPermissions.OrganizationalUnitsCreate)]
    public async Task<ActionResult<CreateAdOrganizationalUnitResponse>> CreateOrganizationalUnit(
        [FromBody] CreateAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await adOrganizationalUnitDirectoryService.CreateOrganizationalUnitAsync(
            new AppModels.CreateAdOrganizationalUnitRequest(
                request.Name,
                request.ParentDistinguishedName,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.OrganizationalUnit is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new CreateAdOrganizationalUnitResponse(
            true,
            AdManagementApiMessageKeys.OrganizationalUnits.CreateSuccess,
            MapOrganizationalUnitDetail(result.OrganizationalUnit)));
    }

    [HttpPut("organizational-units/{id}/rename")]
    [RequirePermission(AdManagementPermissions.OrganizationalUnitsUpdate)]
    public async Task<ActionResult<RenameAdOrganizationalUnitResponse>> RenameOrganizationalUnit(
        [FromRoute] string id,
        [FromBody] RenameAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.OrganizationalUnits.InvalidOrganizationalUnitId });
        }

        var result = await adOrganizationalUnitDirectoryService.RenameOrganizationalUnitAsync(
            new AppModels.RenameAdOrganizationalUnitRequest(
                objectGuid,
                request.Name,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.OrganizationalUnit is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new RenameAdOrganizationalUnitResponse(
            true,
            AdManagementApiMessageKeys.OrganizationalUnits.RenameSuccess,
            MapOrganizationalUnitDetail(result.OrganizationalUnit),
            result.PreviousDistinguishedName));
    }

    [HttpPost("organizational-units/{id}/move")]
    [RequirePermission(AdManagementPermissions.OrganizationalUnitsMove)]
    public async Task<ActionResult<MoveAdOrganizationalUnitResponse>> MoveOrganizationalUnit(
        [FromRoute] string id,
        [FromBody] MoveAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.OrganizationalUnits.InvalidOrganizationalUnitId });
        }

        var result = await adOrganizationalUnitDirectoryService.MoveOrganizationalUnitAsync(
            new AppModels.MoveAdOrganizationalUnitRequest(
                objectGuid,
                request.TargetParentDistinguishedName,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.OrganizationalUnit is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new MoveAdOrganizationalUnitResponse(
            true,
            AdManagementApiMessageKeys.OrganizationalUnits.MoveSuccess,
            MapOrganizationalUnitDetail(result.OrganizationalUnit),
            result.PreviousDistinguishedName,
            result.TargetParentDistinguishedName));
    }

    [HttpDelete("organizational-units/{id}")]
    [RequirePermission(AdManagementPermissions.OrganizationalUnitsDelete)]
    public async Task<ActionResult<DeleteAdOrganizationalUnitResponse>> DeleteOrganizationalUnit(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.OrganizationalUnits.InvalidOrganizationalUnitId });
        }

        var result = await adOrganizationalUnitDirectoryService.DeleteOrganizationalUnitAsync(
            new AppModels.DeleteAdOrganizationalUnitRequest(
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

        return Ok(new DeleteAdOrganizationalUnitResponse(
            true,
            AdManagementApiMessageKeys.OrganizationalUnits.DeleteSuccess,
            result.DeletedOrganizationalUnitId));
    }
}
