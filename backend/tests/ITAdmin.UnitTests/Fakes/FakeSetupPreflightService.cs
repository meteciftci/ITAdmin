using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.UnitTests.Fakes;

public sealed class FakeSetupPreflightService : ISetupPreflightService
{
    public SetupPreflightResult Result { get; set; } = new(Array.Empty<SetupPreflightCheck>(), true);
    public int CheckCallCount { get; private set; }

    public Task<SetupPreflightResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        CheckCallCount++;
        return Task.FromResult(Result);
    }
}
