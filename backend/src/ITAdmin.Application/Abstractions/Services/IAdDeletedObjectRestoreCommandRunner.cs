using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdDeletedObjectRestoreCommandRunner
{
    Task<AdDeletedObjectRestoreCommandResult> ExecuteRestoreAsync(
        AdDeletedObjectRestoreCommandRequest request,
        CancellationToken cancellationToken = default);
}
