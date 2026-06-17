namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdComputerAccountOperationResponse(
    bool Success,
    string MessageKey,
    AdComputerDetailResponse? Computer,
    IReadOnlyDictionary<string, object>? MessageParams = null);
