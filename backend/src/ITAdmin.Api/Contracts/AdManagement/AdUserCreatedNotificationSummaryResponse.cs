namespace ITAdmin.Api.Contracts.AdManagement;

public sealed record AdUserCreatedNotificationSummaryResponse(
    int QueuedCount,
    int SkippedCount,
    IReadOnlyList<string> Messages);
