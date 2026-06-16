using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdDeletedObjectRestoreReadinessService
{
    Task<AdDeletedObjectRestoreReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}
