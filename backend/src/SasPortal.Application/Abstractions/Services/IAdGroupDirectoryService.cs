using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdGroupDirectoryService
{
    Task<AdGroupDirectoryListResult> SearchGroupsAsync(
        AdGroupListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdGroupDirectoryDetailResult> GetGroupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
