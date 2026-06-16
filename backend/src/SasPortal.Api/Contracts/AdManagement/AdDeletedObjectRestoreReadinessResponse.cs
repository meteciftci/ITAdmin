namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdDeletedObjectRestoreReadinessCheckResponse(
    string Key,
    string Status,
    string Title,
    string? Message,
    string? Remediation,
    string? Command,
    bool IsBlocking,
    string? Details);

public sealed record AdDeletedObjectRestoreReadinessResponse(
    bool IsReady,
    string Status,
    string SummaryMessage,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheckResponse> BlockingReasons,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheckResponse> Warnings,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheckResponse> Checks,
    DateTimeOffset CheckedAtUtc,
    string? DomainController);
