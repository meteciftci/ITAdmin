namespace ITAdmin.Application.Common.Models.Notifications;

public sealed record UpdateEmailProviderSettingsRequest(
    bool IsEnabled,
    string? DisplayName,
    string Host,
    int Port,
    bool UseSsl,
    string? UserName,
    string? Password,
    string FromAddress,
    string? FromDisplayName,
    int TimeoutSeconds,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
