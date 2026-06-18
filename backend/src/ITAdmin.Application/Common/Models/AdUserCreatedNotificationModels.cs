namespace ITAdmin.Application.Common.Models;

public sealed record AdUserCreatedNotificationEnqueueRequest(
    CreateAdUserRequest CreateRequest,
    CreateAdUserResponse CreatedUser,
    IReadOnlyList<AdAttributeMappingItem> AttributeMappings,
    string? ActorUserName);
