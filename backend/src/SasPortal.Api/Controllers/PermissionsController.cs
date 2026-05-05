using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.Common;
using SasPortal.Api.Contracts.Permissions;
using SasPortal.Application.Abstractions.Services;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public sealed class PermissionsController(IPermissionService permissionService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Permissions.View")]
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
    [RequirePermission("Permissions.View")]
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
