using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IPermissionService
{
    Task<PagedResult<PermissionListItem>> GetPermissionsAsync(
        PermissionListQuery query,
        CancellationToken cancellationToken = default);

    Task<PermissionDetail?> GetPermissionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
