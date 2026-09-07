namespace ITAdmin.Application.Common.Constants;

public static class NotificationProviderKeys
{
    public const string CustomHttp = "custom-http";
    public const string Teknomart = "teknomart";
    public const string Smtp = "smtp";

    public static readonly IReadOnlyList<string> SmsProviders = [CustomHttp, Teknomart];

    public static bool IsKnownSmsProvider(string? providerKey) =>
        !string.IsNullOrWhiteSpace(providerKey)
        && SmsProviders.Contains(providerKey, StringComparer.OrdinalIgnoreCase);
}
