using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface ISetupService
{
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);

    Task<ValidateSetupLdapResult> ValidateLdapAsync(
        ValidateSetupLdapRequest request,
        CancellationToken cancellationToken = default);

    Task<SearchSetupAdminUsersResult> SearchAdminUsersAsync(
        SearchSetupAdminUsersRequest request,
        CancellationToken cancellationToken = default);

    Task<SearchSetupOrganizationalUnitsResult> SearchOrganizationalUnitsAsync(
        SearchSetupOrganizationalUnitsRequest request,
        CancellationToken cancellationToken = default);

    Task<CompleteSetupResult> CompleteSetupAsync(
        CompleteSetupRequest request,
        CancellationToken cancellationToken = default);
}
