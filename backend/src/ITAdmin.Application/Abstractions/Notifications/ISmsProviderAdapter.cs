using ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Application.Abstractions.Notifications;

public sealed record SmsSendRequest(string PhoneNumber, string Message);

public sealed record SmsProviderRuntimeSettings(
    SmsCustomHttpPublicSettings Public,
    SmsCustomHttpSecretSettings Secrets);

public sealed record SmsProviderDefinition(string ProviderKey, string DisplayName);

public sealed record SmsSendResult(bool IsSuccess, string Message, string? ProviderSummary = null);

public interface ISmsProviderAdapter
{
    string ProviderKey { get; }
    string DisplayName { get; }
    SmsProviderDefinition GetDefinition();
    Task<SmsSendResult> SendAsync(
        SmsSendRequest request,
        SmsProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default);
    Task<SmsSendResult> ValidateAsync(
        SmsProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default);
}
