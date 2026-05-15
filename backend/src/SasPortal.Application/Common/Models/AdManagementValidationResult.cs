namespace SasPortal.Application.Common.Models;

public sealed record AdManagementValidationDetail(
    string Key,
    string Status,
    string? Message);

public sealed record AdManagementValidationResult(
    bool IsValid,
    string Message,
    DateTimeOffset CheckedAt,
    IReadOnlyList<AdManagementValidationDetail> Details);
