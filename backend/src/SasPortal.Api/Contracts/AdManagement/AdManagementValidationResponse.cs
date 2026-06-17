namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdManagementValidationDetailResponse(
    string Key,
    string Status,
    string? Message,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdManagementValidationResponse(
    bool IsValid,
    string Message,
    DateTimeOffset CheckedAt,
    IReadOnlyList<AdManagementValidationDetailResponse> Details,
    AdDeletedObjectRestoreReadinessResponse? RestoreReadiness = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
