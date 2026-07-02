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
public sealed class AdDeletedObjectsController(
    IAdDeletedObjectDirectoryService adDeletedObjectDirectoryService,
    IAdDeletedObjectRestoreService adDeletedObjectRestoreService,
    IAdDeletedObjectRestoreReadinessService adDeletedObjectRestoreReadinessService) : AdManagementControllerBase
{

    [HttpGet("deleted-objects/restore-readiness")]
    [RequirePermission(AdManagementPermissions.DeletedObjectsRestore)]
    public async Task<ActionResult<AdDeletedObjectRestoreReadinessResponse>> GetDeletedObjectRestoreReadiness(
        CancellationToken cancellationToken)
    {
        var result = await adDeletedObjectRestoreReadinessService.CheckAsync(cancellationToken);
        return Ok(MapRestoreReadiness(result));
    }

    [HttpGet("deleted-objects")]
    [RequirePermission(AdManagementPermissions.DeletedObjectsView)]
    public async Task<ActionResult<AdDeletedObjectListResponse>> ListDeletedObjects(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAll = false,
        CancellationToken cancellationToken = default)
    {
        var result = await adDeletedObjectDirectoryService.SearchDeletedObjectsAsync(
            new AppModels.AdDeletedObjectSearchQuery(
                search,
                ParseDeletedObjectTypeFilter(type),
                pageNumber,
                pageSize,
                includeAll),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdDeletedObjectListResponse(
            result.Page.Items.Select(MapDeletedObjectListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("deleted-objects/{id}")]
    [RequirePermission(AdManagementPermissions.DeletedObjectsView)]
    public async Task<ActionResult<AdDeletedObjectDetailResponse>> GetDeletedObjectById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.DeletedObjects.NotFound });
        }

        var result = await adDeletedObjectDirectoryService.GetDeletedObjectByIdAsync(
            objectGuid,
            cancellationToken);
        if (!result.IsSuccess || result.Object is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(MapDeletedObjectDetail(result.Object));
    }

    [HttpPost("deleted-objects/{id}/restore")]
    [RequirePermission(AdManagementPermissions.DeletedObjectsRestore)]
    public async Task<ActionResult<AdDeletedObjectRestoreResponse>> RestoreDeletedObject(
        [FromRoute] string id,
        [FromBody] AdDeletedObjectRestoreRequestBody? body,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.DeletedObjects.NotFound });
        }

        if (!AdDeletedObjectRestoreTargetModeParser.TryParse(
                body?.RestoreTargetMode,
                out var restoreTargetMode))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.Common.InvalidRequest });
        }

        if (restoreTargetMode == AppModels.AdDeletedObjectRestoreTargetMode.TargetPath
            && string.IsNullOrWhiteSpace(body?.TargetPathDistinguishedName))
        {
            return BadRequest(new { messageKey = AdManagementApiMessageKeys.DeletedObjects.RestoreMissingTarget });
        }

        var result = await adDeletedObjectRestoreService.RestoreDeletedObjectAsync(
            new AppModels.AdDeletedObjectRestoreRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent(),
                restoreTargetMode,
                body?.TargetPathDistinguishedName?.Trim()),
            cancellationToken);

        if (!result.IsSuccess || result.RestoredObject is null)
        {
            return MapDirectoryFailure(result.MessageKey, result.FailureKind, result.MessageParams);
        }

        return Ok(new AdDeletedObjectRestoreResponse(
            true,
            result.MessageKey,
            result.RestoredObject.ObjectId,
            result.RestoredObject.ObjectType.ToString(),
            result.RestoredObject.Name,
            result.RestoredObject.SamAccountName,
            result.RestoredObject.DistinguishedName,
            result.RestoredObject.RestoredParent,
            result.MessageParams));
    }

    private static AppModels.AdDeletedObjectTypeFilter ParseDeletedObjectTypeFilter(string? type) =>
        type?.Trim().ToLowerInvariant() switch
        {
            "user" => AppModels.AdDeletedObjectTypeFilter.User,
            "group" => AppModels.AdDeletedObjectTypeFilter.Group,
            "computer" => AppModels.AdDeletedObjectTypeFilter.Computer,
            _ => AppModels.AdDeletedObjectTypeFilter.All,
        };
}
