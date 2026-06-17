namespace SasPortal.Application.Common.Models;

public sealed record AdManagementValidationDetail(
    string Key,
    string Status,
    string? Message,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdManagementValidationResult(
    bool IsValid,
    string Message,
    DateTimeOffset CheckedAt,
    IReadOnlyList<AdManagementValidationDetail> Details,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
