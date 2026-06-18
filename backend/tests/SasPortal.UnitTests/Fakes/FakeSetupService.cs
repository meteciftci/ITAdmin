using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.Fakes;

public sealed class FakeSetupService : ISetupService
{
    public bool IsSetupRequiredResult { get; set; } = true;
    public int IsSetupRequiredCallCount { get; private set; }

    public CompleteSetupResult CompleteSetupResult { get; set; } = new(true, "ok");
    public int CompleteSetupCallCount { get; private set; }
    public CompleteSetupRequest? LastCompleteSetupRequest { get; private set; }

    public Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        IsSetupRequiredCallCount++;
        return Task.FromResult(IsSetupRequiredResult);
    }

    public Task<CompleteSetupResult> CompleteSetupAsync(
        CompleteSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        CompleteSetupCallCount++;
        LastCompleteSetupRequest = request;
        return Task.FromResult(CompleteSetupResult);
    }
}
