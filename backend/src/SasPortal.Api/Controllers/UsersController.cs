using System.Security.Claims;
using SasPortal.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.Common;
using SasPortal.Api.Contracts.Users;
using SasPortal.Application.Abstractions.Services;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.Users.View)]
    public async Task<ActionResult<PagedResponse<UserListItemResponse>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await userService.GetUsersAsync(
            new AppModels.UserListQuery(search, isActive, pageNumber, pageSize),
            cancellationToken);

        var response = new PagedResponse<UserListItemResponse>(
            result.Items.Select(x => new UserListItemResponse(
                x.Id,
                x.DirectorySource,
                x.DirectoryObjectId,
                x.UserName,
                x.DisplayName,
                x.NationalIdMasked,
                x.Email,
                x.IsActive,
                x.LastLoginAt,
                x.Roles)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.Users.Create)]
    public async Task<ActionResult<UserDetailResponse>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.CreateUserAsync(
            new AppModels.CreateUserRequest(
                request.DirectoryObjectId,
                request.IsActive,
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        if (result.User is null)
        {
            return BadRequest(new { message = "User could not be created." });
        }

        var u = result.User;
        var response = new UserDetailResponse(
            u.Id,
            u.DirectorySource,
            u.DirectoryObjectId,
            u.UserName,
            u.DisplayName,
            u.NationalIdMasked,
            u.Email,
            u.IsActive,
            u.LastLoginAt,
            u.Roles,
            u.CreatedAt,
            u.CreatedBy,
            u.UpdatedAt,
            u.UpdatedBy);

        return CreatedAtAction(nameof(GetUserById), new { id = response.Id }, response);
    }

    [HttpGet("lookup-directory")]
    [RequirePermission(PermissionCodes.Users.Create)]
    public async Task<ActionResult<UserDirectoryLookupResponse>> LookupDirectoryUsers(
        [FromQuery] string search,
        [FromQuery] int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
        {
            return BadRequest(new { message = "Search term must be at least 2 characters." });
        }

        var result = await userService.LookupDirectoryUsersAsync(
            new AppModels.UserDirectoryLookupQuery(search.Trim(), maxResults),
            cancellationToken);

        return Ok(new UserDirectoryLookupResponse(
            result.Items
                .Select(x => new UserDirectoryLookupItemResponse(
                    x.DirectoryObjectId,
                    x.UserName,
                    x.DisplayName,
                    x.Email,
                    x.NationalIdMasked,
                    x.IsAlreadyPortalUser))
                .ToList()));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.Users.View)]
    public async Task<ActionResult<UserDetailResponse>> GetUserById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await userService.GetUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new UserDetailResponse(
            user.Id,
            user.DirectorySource,
            user.DirectoryObjectId,
            user.UserName,
            user.DisplayName,
            user.NationalIdMasked,
            user.Email,
            user.IsActive,
            user.LastLoginAt,
            user.Roles,
            user.CreatedAt,
            user.CreatedBy,
            user.UpdatedAt,
            user.UpdatedBy));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(PermissionCodes.Users.Update)]
    public async Task<ActionResult<UserDetailResponse>> UpdateUserStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.UpdateUserStatusAsync(
            new AppModels.UpdateUserStatusRequest(
                id,
                request.IsActive,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (string.Equals(result.Message, "User was not found.", StringComparison.Ordinal))
            {
                return NotFound();
            }

            return BadRequest(new { message = result.Message });
        }

        if (result.User is null)
        {
            return BadRequest(new { message = "User status could not be updated." });
        }

        var u = result.User;
        return Ok(new UserDetailResponse(
            u.Id,
            u.DirectorySource,
            u.DirectoryObjectId,
            u.UserName,
            u.DisplayName,
            u.NationalIdMasked,
            u.Email,
            u.IsActive,
            u.LastLoginAt,
            u.Roles,
            u.CreatedAt,
            u.CreatedBy,
            u.UpdatedAt,
            u.UpdatedBy));
    }

    [HttpPut("{id:guid}/roles")]
    [RequirePermission(PermissionCodes.Users.AssignRoles)]
    public async Task<ActionResult<UserDetailResponse>> UpdateUserRoles(
        Guid id,
        [FromBody] UpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.UpdateUserRolesAsync(
            new AppModels.UpdateUserRolesRequest(
                id,
                request.RoleIds,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (string.Equals(result.Message, "User was not found.", StringComparison.Ordinal))
            {
                return NotFound();
            }

            return BadRequest(new { message = result.Message });
        }

        if (result.User is null)
        {
            return BadRequest(new { message = "User roles could not be updated." });
        }

        var u = result.User;
        return Ok(new UserDetailResponse(
            u.Id,
            u.DirectorySource,
            u.DirectoryObjectId,
            u.UserName,
            u.DisplayName,
            u.NationalIdMasked,
            u.Email,
            u.IsActive,
            u.LastLoginAt,
            u.Roles,
            u.CreatedAt,
            u.CreatedBy,
            u.UpdatedAt,
            u.UpdatedBy));
    }

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
            ?? principal.FindFirst(JwtSubClaimType)?.Value;

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

    private const string JwtSubClaimType = "sub";
}
