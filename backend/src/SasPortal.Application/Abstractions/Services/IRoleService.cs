using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IRoleService
{
    Task<PagedResult<RoleListItem>> GetRolesAsync(RoleListQuery query, CancellationToken cancellationToken = default);
    Task<RoleDetail?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CreateRoleResult> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<UpdateRoleResult> UpdateRoleAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task<UpdateRoleStatusResult> UpdateRoleStatusAsync(
        UpdateRoleStatusRequest request,
        CancellationToken cancellationToken = default);
}
