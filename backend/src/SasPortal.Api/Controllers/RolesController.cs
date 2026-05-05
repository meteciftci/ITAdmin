using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

        return Ok(MapRoleDetail(role));
    }

    [HttpPost]
    [RequirePermission("Roles.Create")]
    public async Task<ActionResult<RoleDetailResponse>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorUserName = GetActorUserName();
        var result = await roleService.CreateRoleAsync(
            new AppModels.CreateRoleRequest(
                request.Name,
                request.Code,
                request.Description,
                request.IsActive,
                actorUserName),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        if (result.Role is null)
        {
            return BadRequest(new { message = "Role could not be created." });
        }

        var response = MapRoleDetail(result.Role);
        return CreatedAtAction(nameof(GetRoleById), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("Roles.Update")]
    public async Task<ActionResult<RoleDetailResponse>> UpdateRole(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorUserName = GetActorUserName();
        var result = await roleService.UpdateRoleAsync(
            new AppModels.UpdateRoleRequest(
                id,
                request.Name,
                request.Description,
                request.IsActive,
                actorUserName),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Message == "Role was not found.")
            {
                return NotFound();
            }

            return BadRequest(new { message = result.Message });
        }

        if (result.Role is null)
        {
            return BadRequest(new { message = "Role could not be updated." });
        }

        return Ok(MapRoleDetail(result.Role));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission("Roles.Update")]
    public async Task<ActionResult<RoleDetailResponse>> UpdateRoleStatus(
        Guid id,
        [FromBody] UpdateRoleStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorUserName = GetActorUserName();
        var result = await roleService.UpdateRoleStatusAsync(
            new AppModels.UpdateRoleStatusRequest(
                id,
                request.IsActive,
                actorUserName),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Message == "Role was not found.")
            {
                return NotFound();
            }

            return BadRequest(new { message = result.Message });
        }

        if (result.Role is null)
        {
            return BadRequest(new { message = "Role status could not be updated." });
        }

        return Ok(MapRoleDetail(result.Role));
    }

    private string? GetActorUserName()
    {
        if (!string.IsNullOrWhiteSpace(User.Identity?.Name))
        {
            return User.Identity!.Name;
        }

        var claimUserName = User.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrWhiteSpace(claimUserName))
        {
            return claimUserName;
        }

        var nameClaim = User.FindFirstValue("name");
        return string.IsNullOrWhiteSpace(nameClaim) ? null : nameClaim;
    }

    private static RoleDetailResponse MapRoleDetail(AppModels.RoleDetail role) =>
        new(
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
            role.UpdatedBy);
}
