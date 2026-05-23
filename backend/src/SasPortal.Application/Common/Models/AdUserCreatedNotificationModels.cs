namespace SasPortal.Application.Common.Models;

public sealed record AdUserCreatedNotificationSummary(
    int QueuedCount,
    int SkippedCount,
    IReadOnlyList<string> Messages);

public sealed record AdUserCreatedNotificationEnqueueRequest(
    CreateAdUserRequest CreateRequest,
    CreateAdUserResponse CreatedUser,
    IReadOnlyList<AdAttributeMappingItem> AttributeMappings,
    string? ActorUserName);
