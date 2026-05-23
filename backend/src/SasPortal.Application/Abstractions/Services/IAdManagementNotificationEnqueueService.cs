using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdManagementNotificationEnqueueService
{
    Task<AdManagementNotificationSummary> EnqueueUserCreatedAsync(
        AdUserCreatedNotificationEnqueueRequest request,
        CancellationToken cancellationToken = default);

    Task<AdManagementNotificationSummary> EnqueueAccountOperationAsync(
        AdManagementAccountOperationNotificationRequest request,
        CancellationToken cancellationToken = default);
}
