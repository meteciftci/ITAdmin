using Microsoft.AspNetCore.Authorization;
using ITAdmin.Application.Common.Security;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.Common;
using ITAdmin.Api.Contracts.Permissions;
using ITAdmin.Application.Abstractions.Services;
using AppModels = ITAdmin.Application.Common.Models;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public sealed class PermissionsController(IPermissionService permissionService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Permissions.View)]
    public async Task<ActionResult<PagedResponse<PermissionListItemResponse>>> GetPermissions(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await permissionService.GetPermissionsAsync(
            new AppModels.PermissionListQuery(search, isActive, pageNumber, pageSize),
            cancellationToken);

        var response = new PagedResponse<PermissionListItemResponse>(
            result.Items.Select(x => new PermissionListItemResponse(
                x.Id,
                x.Module,
                x.Name,
                x.Code,
                x.Description,
                x.IsActive)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Permissions.View)]
    public async Task<ActionResult<PermissionDetailResponse>> GetPermissionById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var permission = await permissionService.GetPermissionByIdAsync(id, cancellationToken);
        if (permission is null)
        {
            return NotFound();
        }

        return Ok(new PermissionDetailResponse(
            permission.Id,
            permission.Module,
            permission.Name,
            permission.Code,
            permission.Description,
            permission.IsActive,
            permission.CreatedAt,
            permission.CreatedBy,
            permission.UpdatedAt,
            permission.UpdatedBy));
    }
}
