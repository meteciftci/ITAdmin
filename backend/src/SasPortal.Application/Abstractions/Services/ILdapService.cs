using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface ILdapService
{
    Task<LdapValidationResult> ValidateAsync(LdapValidationRequest request, CancellationToken cancellationToken = default);

    Task<LdapUserProfile?> GetUserProfileAsync(
        LdapUserProfileRequest request,
        CancellationToken cancellationToken = default);
}
