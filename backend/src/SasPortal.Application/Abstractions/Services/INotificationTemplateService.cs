using SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Application.Abstractions.Services;

public interface INotificationTemplateService
{
    Task<IReadOnlyList<NotificationTemplateListItem>> GetListAsync(
        NotificationTemplateListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<NotificationTemplateOperationResult> CreateAsync(
        CreateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateOperationResult> UpdateAsync(
        Guid id,
        UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);
}
