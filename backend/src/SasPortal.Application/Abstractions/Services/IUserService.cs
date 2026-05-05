using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IUserService
{
    Task<PagedResult<UserListItem>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default);
    Task<UserDetail?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserDirectoryLookupResult> LookupDirectoryUsersAsync(
        UserDirectoryLookupQuery query,
        CancellationToken cancellationToken = default);

    Task<CreateUserResult> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateUserStatusResult> UpdateUserStatusAsync(
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateUserRolesResult> UpdateUserRolesAsync(
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken = default);
}
