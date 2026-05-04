using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IUserService
{
    Task<PagedResult<UserListItem>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken = default);
    Task<UserDetail?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserDirectoryLookupResult> LookupDirectoryUsersAsync(
        UserDirectoryLookupQuery query,
        CancellationToken cancellationToken = default);
}
