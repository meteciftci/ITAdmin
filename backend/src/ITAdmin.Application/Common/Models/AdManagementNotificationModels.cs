namespace ITAdmin.Application.Common.Models;

public sealed record AdManagementNotificationSummary(
    int QueuedCount,
    int SkippedCount,
    IReadOnlyList<string> Messages);

public sealed record AdManagementNotificationUserContext(
    string UserId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? Mail,
    string? Department,
    IReadOnlyDictionary<string, string> MappedValuesByLogicalField,
    IReadOnlyDictionary<string, string> AttributeValuesByName,
    IReadOnlyList<AdAttributeMappingItem> AttributeMappings,
    string? ActorUserName);

public sealed record AdManagementAccountOperationNotificationRequest(
    string EventKey,
    AdManagementNotificationUserContext UserContext);
