using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdDeletedObjectRestoreService
{
    Task<AdDeletedObjectRestoreResult> RestoreDeletedObjectAsync(
        AdDeletedObjectRestoreRequest request,
        CancellationToken cancellationToken = default);
}
