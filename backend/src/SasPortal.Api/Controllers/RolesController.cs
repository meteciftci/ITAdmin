using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.Common;
using SasPortal.Api.Contracts.Roles;
using SasPortal.Application.Abstractions.Services;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public sealed class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Roles.View")]
    public async Task<ActionResult<PagedResponse<RoleListItemResponse>>> GetRoles(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isSystem,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await roleService.GetRolesAsync(
            new AppModels.RoleListQuery(search, isActive, isSystem, pageNumber, pageSize),
            cancellationToken);

        var response = new PagedResponse<RoleListItemResponse>(
            result.Items.Select(x => new RoleListItemResponse(
                x.Id,
                x.Name,
                x.Code,
                x.Description,
                x.IsSystem,
                x.IsActive,
                x.PermissionCount)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("Roles.View")]
    public async Task<ActionResult<RoleDetailResponse>> GetRoleById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var role = await roleService.GetRoleByIdAsync(id, cancellationToken);
        if (role is null)
        {
            return NotFound();
        }

        return Ok(new RoleDetailResponse(
            role.Id,
            role.Name,
            role.Code,
            role.Description,
            role.IsSystem,
            role.IsActive,
            role.Permissions
                .Select(x => new RolePermissionItemResponse(
                    x.Id,
                    x.Name,
                    x.Code,
                    x.Description,
                    x.IsActive))
                .ToList(),
            role.CreatedAt,
            role.CreatedBy,
            role.UpdatedAt,
            role.UpdatedBy));
    }
}
