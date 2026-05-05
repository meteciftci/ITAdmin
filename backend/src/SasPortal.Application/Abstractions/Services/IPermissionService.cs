using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IPermissionService
{
    Task<PagedResult<PermissionListItem>> GetPermissionsAsync(
        PermissionListQuery query,
        CancellationToken cancellationToken = default);

    Task<PermissionDetail?> GetPermissionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
