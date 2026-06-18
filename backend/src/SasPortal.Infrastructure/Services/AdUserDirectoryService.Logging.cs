using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace SasPortal.Infrastructure.Services;

public sealed partial class AdUserDirectoryService
{
    private void LogUnexpectedDirectoryFailure(
        Exception ex,
        Guid? actorUserId = null,
        [CallerMemberName] string operationName = "")
    {
        logger.LogError(
            ex,
            "AD directory operation unexpected failure. Operation={OperationName} ActorUserId={ActorUserId}",
            operationName,
            actorUserId);
    }

    private void LogBestEffortDirectoryFailure(
        Exception ex,
        [CallerMemberName] string operationName = "")
    {
        logger.LogWarning(
            ex,
            "AD directory operation best-effort step failed. Operation={OperationName}",
            operationName);
    }
}
