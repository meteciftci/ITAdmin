using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.UnitTests.Fakes;

public sealed class FakeSetupService : ISetupService
{
    public bool IsSetupRequiredResult { get; set; } = true;
    public CompleteSetupResult CompleteSetupResult { get; set; } = new(true, "ok");
    public ValidateSetupLdapResult ValidateLdapResult { get; set; } = new(true, "ok");
    public SearchSetupAdminUsersResult SearchAdminUsersResult { get; set; } = new([], null);
    public SearchSetupOrganizationalUnitsResult SearchOrganizationalUnitsResult { get; set; } =
        new([], false, null);
    public int CompleteSetupCallCount { get; private set; }
    public int ValidateLdapCallCount { get; private set; }
    public int SearchAdminUsersCallCount { get; private set; }
    public int SearchOrganizationalUnitsCallCount { get; private set; }
    public CompleteSetupRequest? LastCompleteSetupRequest { get; private set; }
    public ValidateSetupLdapRequest? LastValidateLdapRequest { get; private set; }
    public SearchSetupAdminUsersRequest? LastSearchAdminUsersRequest { get; private set; }
    public SearchSetupOrganizationalUnitsRequest? LastSearchOrganizationalUnitsRequest { get; private set; }

    public Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(IsSetupRequiredResult);

    public Task<CompleteSetupResult> CompleteSetupAsync(
        CompleteSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        CompleteSetupCallCount++;
        LastCompleteSetupRequest = request;
        return Task.FromResult(CompleteSetupResult);
    }

    public Task<ValidateSetupLdapResult> ValidateLdapAsync(
        ValidateSetupLdapRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateLdapCallCount++;
        LastValidateLdapRequest = request;
        return Task.FromResult(ValidateLdapResult);
    }

    public Task<SearchSetupAdminUsersResult> SearchAdminUsersAsync(
        SearchSetupAdminUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        SearchAdminUsersCallCount++;
        LastSearchAdminUsersRequest = request;
        return Task.FromResult(SearchAdminUsersResult);
    }

    public Task<SearchSetupOrganizationalUnitsResult> SearchOrganizationalUnitsAsync(
        SearchSetupOrganizationalUnitsRequest request,
        CancellationToken cancellationToken = default)
    {
        SearchOrganizationalUnitsCallCount++;
        LastSearchOrganizationalUnitsRequest = request;
        return Task.FromResult(SearchOrganizationalUnitsResult);
    }
}
