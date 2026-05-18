using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdUserDirectoryService
{
    Task<AdUserDirectorySearchResult> SearchUsersAsync(
        AdUserSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<AdUserDirectoryDetailResult> GetUserByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
