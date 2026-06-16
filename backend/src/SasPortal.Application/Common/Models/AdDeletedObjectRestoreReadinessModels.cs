namespace SasPortal.Application.Common.Models;

public sealed record AdDeletedObjectRestoreReadinessCheck(
    string Key,
    string Status,
    string Title,
    string? Message,
    string? Remediation,
    string? Command,
    bool IsBlocking,
    string? Details);

public sealed record AdDeletedObjectRestoreReadinessResult(
    bool IsReady,
    string Status,
    string SummaryMessage,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheck> BlockingReasons,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheck> Warnings,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheck> Checks,
    DateTimeOffset CheckedAtUtc,
    string? DomainController);
