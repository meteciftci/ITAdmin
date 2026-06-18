using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAdManagementNotificationEnqueueService
{
    Task<AdManagementNotificationSummary> EnqueueUserCreatedAsync(
        AdUserCreatedNotificationEnqueueRequest request,
        CancellationToken cancellationToken = default);

    Task<AdManagementNotificationSummary> EnqueueAccountOperationAsync(
        AdManagementAccountOperationNotificationRequest request,
        CancellationToken cancellationToken = default);
}
