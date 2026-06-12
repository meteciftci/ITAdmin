namespace SasPortal.Api.Contracts.AdManagement;

public sealed record DeleteAdComputerResponse(
    bool Success,
    string Message,
    string? DeletedComputerId,
    string? DeletedComputerName,
    string? DeletedDistinguishedName);
