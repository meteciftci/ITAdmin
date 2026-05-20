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

    Task<AdOrganizationalUnitSearchResult> SearchOrganizationalUnitsAsync(
        AdOrganizationalUnitSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<CreateAdUserResult> CreateUserAsync(
        CreateAdUserRequest request,
        CancellationToken cancellationToken = default);

    Task<AdUpnSuffixesResult> GetUpnSuffixesAsync(CancellationToken cancellationToken = default);
}
