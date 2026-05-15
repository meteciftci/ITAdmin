namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdManagementValidationDetailResponse(
    string Key,
    string Status,
    string? Message);

public sealed record AdManagementValidationResponse(
    bool IsValid,
    string Message,
    DateTimeOffset CheckedAt,
    IReadOnlyList<AdManagementValidationDetailResponse> Details);
