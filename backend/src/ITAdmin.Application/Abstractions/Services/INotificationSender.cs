using ITAdmin.Application.Abstractions.Notifications;

namespace ITAdmin.Application.Abstractions.Services;

public interface INotificationSender
{
    Task<SmsSendResult> SendSmsAsync(SmsSendRequest request, CancellationToken cancellationToken = default);
    Task<EmailSendResult> SendEmailAsync(EmailSendRequest request, CancellationToken cancellationToken = default);
}
