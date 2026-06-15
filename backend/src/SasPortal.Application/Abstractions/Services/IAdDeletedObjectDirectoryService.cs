using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdDeletedObjectDirectoryService
{
    Task<AdDeletedObjectSearchResult> SearchDeletedObjectsAsync(
        AdDeletedObjectSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<AdDeletedObjectDetailResult> GetDeletedObjectByIdAsync(
        Guid objectGuid,
        CancellationToken cancellationToken = default);
}
