using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.Fakes;

public sealed class FakeLdapService : ILdapService
{
    public int ValidateBindCallCount { get; private set; }
    public int ValidateCallCount { get; private set; }
    public LdapBindValidationRequest? LastValidateBindRequest { get; private set; }
    public LdapValidationRequest? LastValidateRequest { get; private set; }

    public LdapValidationResult ValidateBindResult { get; set; } = new(true, "bind ok");
    public LdapValidationResult ValidateResult { get; set; } = new(true, "full ok");

    public Task<LdapValidationResult> ValidateBindAsync(LdapBindValidationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateBindCallCount++;
        LastValidateBindRequest = request;
        return Task.FromResult(ValidateBindResult);
    }

    public Task<LdapValidationResult> ValidateAsync(LdapValidationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCallCount++;
        LastValidateRequest = request;
        return Task.FromResult(ValidateResult);
    }

    public Task<LdapUserProfile?> GetUserProfileAsync(
        LdapUserProfileRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult<LdapUserProfile?>(null);

    public Task<LdapUserProfile?> GetUserProfileByObjectIdAsync(
        LdapUserProfileByObjectIdRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult<LdapUserProfile?>(null);

    public Task<IReadOnlyCollection<LdapUserLookupItem>> SearchUsersAsync(
        LdapUserLookupRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
}
