namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdComputerAccountOperationResponse(
    bool Success,
    string Message,
    AdComputerDetailResponse? Computer,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
