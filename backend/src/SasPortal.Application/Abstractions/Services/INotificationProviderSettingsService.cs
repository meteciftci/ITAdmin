using SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Application.Abstractions.Services;

public interface INotificationProviderSettingsService
{
    Task<SmsProviderSettingsResponse> GetSmsSettingsAsync(CancellationToken cancellationToken = default);
    Task<EmailProviderSettingsResponse> GetEmailSettingsAsync(CancellationToken cancellationToken = default);
    Task<NotificationProviderOperationResult> UpdateSmsSettingsAsync(
        UpdateSmsProviderSettingsRequest request,
        CancellationToken cancellationToken = default);
    Task<NotificationProviderOperationResult> UpdateEmailSettingsAsync(
        UpdateEmailProviderSettingsRequest request,
        CancellationToken cancellationToken = default);
    Task<NotificationProviderOperationResult> TestSmsAsync(
        TestSmsProviderRequest request,
        CancellationToken cancellationToken = default);
    Task<NotificationProviderOperationResult> TestEmailAsync(
        TestEmailProviderRequest request,
        CancellationToken cancellationToken = default);
}
