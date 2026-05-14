using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.Fakes;

public sealed class FakeLdapService : ILdapService
{
    public int ValidateBindCallCount { get; private set; }
    public int ValidateSearchBasesCallCount { get; private set; }
    public int ValidateCallCount { get; private set; }
    public int GetUserProfileCallCount { get; private set; }
    public LdapBindValidationRequest? LastValidateBindRequest { get; private set; }
    public LdapSearchBasesValidationRequest? LastValidateSearchBasesRequest { get; private set; }
    public LdapValidationRequest? LastValidateRequest { get; private set; }
    public LdapUserProfileRequest? LastGetUserProfileRequest { get; private set; }

    public LdapValidationResult ValidateBindResult { get; set; } = new(true, "bind ok");
    public LdapValidationResult ValidateSearchBasesResult { get; set; } = new(true, "LDAP bind validation succeeded.");
    public LdapValidationResult ValidateResult { get; set; } = new(true, "full ok");
    public LdapUserProfile? UserProfileResult { get; set; }
    public LdapUserProfile? UserProfileByObjectIdResult { get; set; }
    public Exception? ValidateException { get; set; }
    public Exception? GetUserProfileException { get; set; }

    public Task<LdapValidationResult> ValidateBindAsync(LdapBindValidationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateBindCallCount++;
        LastValidateBindRequest = request;
        return Task.FromResult(ValidateBindResult);
    }

    public Task<LdapValidationResult> ValidateSearchBasesAsync(
        LdapSearchBasesValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateSearchBasesCallCount++;
        LastValidateSearchBasesRequest = request;
        return Task.FromResult(ValidateSearchBasesResult);
    }

    public Task<LdapValidationResult> ValidateAsync(LdapValidationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCallCount++;
        LastValidateRequest = request;
        if (ValidateException is not null)
        {
            throw ValidateException;
        }

        return Task.FromResult(ValidateResult);
    }

    public Task<LdapUserProfile?> GetUserProfileAsync(
        LdapUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        GetUserProfileCallCount++;
        LastGetUserProfileRequest = request;
        if (GetUserProfileException is not null)
        {
            throw GetUserProfileException;
        }

        return Task.FromResult(UserProfileResult);
    }

    public Task<LdapUserProfile?> GetUserProfileByObjectIdAsync(
        LdapUserProfileByObjectIdRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UserProfileByObjectIdResult);
    }

    public Task<IReadOnlyCollection<LdapUserLookupItem>> SearchUsersAsync(
        LdapUserLookupRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<LdapUserLookupItem>>(Array.Empty<LdapUserLookupItem>());
}
