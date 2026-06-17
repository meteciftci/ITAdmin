namespace SasPortal.Application.Common.Models;

public sealed record AdManagementValidationDetail(
    string Key,
    string Status,
    string? MessageKey,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdManagementValidationResult(
    bool IsValid,
    string MessageKey,
    DateTimeOffset CheckedAt,
    IReadOnlyList<AdManagementValidationDetail> Details,
    IReadOnlyDictionary<string, object>? MessageParams = null);
