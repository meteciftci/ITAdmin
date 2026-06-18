using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IReadinessService
{
    Task<ReadinessDatabaseResult> CheckDatabaseAsync(CancellationToken cancellationToken = default);

    Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}
