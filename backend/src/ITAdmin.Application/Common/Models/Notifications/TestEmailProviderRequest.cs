namespace ITAdmin.Application.Common.Models.Notifications;

public sealed record TestEmailProviderRequest(
    string RecipientEmail,
    string Subject,
    string Body,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
