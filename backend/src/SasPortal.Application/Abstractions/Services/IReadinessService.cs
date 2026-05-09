using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IReadinessService
{
    Task<ReadinessDatabaseResult> CheckDatabaseAsync(CancellationToken cancellationToken = default);

    Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}
