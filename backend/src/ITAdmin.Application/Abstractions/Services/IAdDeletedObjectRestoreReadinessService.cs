using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdDeletedObjectRestoreReadinessService
{
    Task<AdDeletedObjectRestoreReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}
