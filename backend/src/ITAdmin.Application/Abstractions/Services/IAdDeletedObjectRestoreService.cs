using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdDeletedObjectRestoreService
{
    Task<AdDeletedObjectRestoreResult> RestoreDeletedObjectAsync(
        AdDeletedObjectRestoreRequest request,
        CancellationToken cancellationToken = default);
}
