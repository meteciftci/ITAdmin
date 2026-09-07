using ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Application.Abstractions.Notifications;

public sealed record SmsSendRequest(
    string PhoneNumber,
    string Message,
    SmsSendKind Kind = SmsSendKind.Default);

/// <summary>
/// A provider's persisted settings, carried as raw JSON so every adapter owns its own shape.
/// <see cref="PublicJson"/> is the stored public settings; <see cref="SecretJson"/> is the
/// already-decrypted secret settings. Both default to <c>{}</c> when nothing is stored yet.
/// </summary>
public sealed record SmsProviderRuntimeSettings(string ProviderKey, string PublicJson, string SecretJson)
{
    public static SmsProviderRuntimeSettings Empty(string providerKey) => new(providerKey, "{}", "{}");
}

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
