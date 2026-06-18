namespace ITAdmin.Application.Common.Models;

public sealed record AdDeletedObjectRestoreReadinessCheck(
    string Key,
    string Status,
    string Title,
    string? Remediation,
    string? Command,
    bool IsBlocking,
    string? Details,
    string TitleKey,
    IReadOnlyDictionary<string, object>? TitleParams,
    string? MessageKey,
    IReadOnlyDictionary<string, object>? MessageParams,
    string? RemediationKey,
    IReadOnlyDictionary<string, object>? RemediationParams);

public sealed record AdDeletedObjectRestoreReadinessResult(
    bool IsReady,
    string Status,
    string SummaryMessage,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheck> BlockingReasons,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheck> Warnings,
    IReadOnlyList<AdDeletedObjectRestoreReadinessCheck> Checks,
    DateTimeOffset CheckedAtUtc,
    string? DomainController,
    string SummaryKey,
    IReadOnlyDictionary<string, object>? SummaryParams);
