using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Application.Abstractions.Services;

public interface INotificationOutboxService
{
    Task<NotificationOutboxEnqueueResult> EnqueueAsync(
        NotificationOutboxEnqueueRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<NotificationOutboxListItem>> GetListAsync(
        NotificationOutboxListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationOutboxDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<NotificationOutboxOperationResult> RetryAsync(
        Guid id,
        NotificationOutboxActorRequest actor,
        CancellationToken cancellationToken = default);

    Task<NotificationOutboxOperationResult> CancelAsync(
        Guid id,
        NotificationOutboxActorRequest actor,
        CancellationToken cancellationToken = default);
}
