using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAdUserCreatedNotificationEnqueueService
{
    Task<AdUserCreatedNotificationSummary> EnqueueUserCreatedAsync(
        AdUserCreatedNotificationEnqueueRequest request,
        CancellationToken cancellationToken = default);
}
