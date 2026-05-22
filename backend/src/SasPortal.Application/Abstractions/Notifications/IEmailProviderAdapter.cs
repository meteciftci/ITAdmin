using SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Application.Abstractions.Notifications;

public sealed record EmailSendRequest(
    string RecipientEmail,
    string Subject,
    string Body);

public sealed record EmailProviderRuntimeSettings(
    EmailSmtpPublicSettings Public,
    EmailSmtpSecretSettings Secrets);

public sealed record EmailProviderDefinition(string ProviderKey, string DisplayName);

public sealed record EmailSendResult(bool IsSuccess, string Message, string? ProviderSummary = null);

public interface IEmailProviderAdapter
{
    string ProviderKey { get; }
    string DisplayName { get; }
    EmailProviderDefinition GetDefinition();
    Task<EmailSendResult> SendAsync(
        EmailSendRequest request,
        EmailProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default);
    Task<EmailSendResult> ValidateAsync(
        EmailProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default);
}
