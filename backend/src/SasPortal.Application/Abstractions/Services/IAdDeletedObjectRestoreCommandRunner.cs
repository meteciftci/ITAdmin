using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdDeletedObjectRestoreCommandRunner
{
    Task<AdDeletedObjectRestoreCommandResult> ExecuteRestoreAsync(
        AdDeletedObjectRestoreCommandRequest request,
        CancellationToken cancellationToken = default);
}
