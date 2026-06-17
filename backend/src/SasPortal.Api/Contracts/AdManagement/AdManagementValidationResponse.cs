namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdManagementValidationDetailResponse(
    string Key,
    string Status,
    string? MessageKey,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record AdManagementValidationResponse(
    bool IsValid,
    string MessageKey,
    DateTimeOffset CheckedAt,
    IReadOnlyList<AdManagementValidationDetailResponse> Details,
    AdDeletedObjectRestoreReadinessResponse? RestoreReadiness = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
