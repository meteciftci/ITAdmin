using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface ILdapService
{
    Task<LdapValidationResult> ValidateBindAsync(LdapBindValidationRequest request, CancellationToken cancellationToken = default);

    Task<LdapValidationResult> ValidateSearchBasesAsync(
        LdapSearchBasesValidationRequest request,
        CancellationToken cancellationToken = default);

    Task<LdapValidationResult> ValidateAsync(LdapValidationRequest request, CancellationToken cancellationToken = default);

    Task<LdapUserProfile?> GetUserProfileAsync(
        LdapUserProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<LdapUserProfile?> GetUserProfileByObjectIdAsync(
        LdapUserProfileByObjectIdRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LdapUserLookupItem>> SearchUsersAsync(
        LdapUserLookupRequest request,
        CancellationToken cancellationToken = default);

    Task<LdapOrganizationalUnitSearchResult> SearchOrganizationalUnitsAsync(
        LdapOrganizationalUnitSearchRequest request,
        CancellationToken cancellationToken = default);
}
