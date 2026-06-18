using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdOrganizationalUnitDirectoryService
{
    Task<AdOrganizationalUnitManageListResult> SearchManageOrganizationalUnitsAsync(
        AdOrganizationalUnitManageListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdOrganizationalUnitDetailResult> GetOrganizationalUnitByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CreateAdOrganizationalUnitResult> CreateOrganizationalUnitAsync(
        CreateAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default);

    Task<RenameAdOrganizationalUnitResult> RenameOrganizationalUnitAsync(
        RenameAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default);

    Task<MoveAdOrganizationalUnitResult> MoveOrganizationalUnitAsync(
        MoveAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default);

    Task<DeleteAdOrganizationalUnitResult> DeleteOrganizationalUnitAsync(
        DeleteAdOrganizationalUnitRequest request,
        CancellationToken cancellationToken = default);
}
