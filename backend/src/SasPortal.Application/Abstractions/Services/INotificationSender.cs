using SasPortal.Application.Abstractions.Notifications;

namespace SasPortal.Application.Abstractions.Services;

public interface INotificationSender
{
    Task<SmsSendResult> SendSmsAsync(SmsSendRequest request, CancellationToken cancellationToken = default);
    Task<EmailSendResult> SendEmailAsync(EmailSendRequest request, CancellationToken cancellationToken = default);
}
