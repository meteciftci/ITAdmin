using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface ISetupPreflightService
{
    Task<SetupPreflightResult> CheckAsync(CancellationToken cancellationToken = default);
}
