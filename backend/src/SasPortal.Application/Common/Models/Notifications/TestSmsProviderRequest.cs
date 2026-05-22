namespace SasPortal.Application.Common.Models.Notifications;

public sealed record TestSmsProviderRequest(
    string PhoneNumber,
    string Message,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
