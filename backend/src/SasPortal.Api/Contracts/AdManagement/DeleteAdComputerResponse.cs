namespace SasPortal.Api.Contracts.AdManagement;

public sealed record DeleteAdComputerResponse(
    bool Success,
    string MessageKey,
    string? DeletedComputerId,
    string? DeletedComputerName,
    string? DeletedDistinguishedName,
    IReadOnlyDictionary<string, object>? MessageParams = null);
