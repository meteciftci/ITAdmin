namespace ITAdmin.Application.Abstractions.Services;

public interface INotificationOutboxBatchProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default);
    Task<int> RecoverStaleProcessingAsync(CancellationToken cancellationToken = default);
}
