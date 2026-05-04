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
    [RequirePermission("Users.View")]
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

    [HttpGet("lookup-directory")]
    [RequirePermission("Users.Create")]
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
    [RequirePermission("Users.View")]
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
}
