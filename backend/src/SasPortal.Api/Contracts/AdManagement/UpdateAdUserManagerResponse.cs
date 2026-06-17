namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdUserManagerResponse(
    bool Success,
    string MessageKey,
    string UserId,
    string? SamAccountName,
    string? ManagerDistinguishedName,
    string? ManagerDisplayName,
    IReadOnlyDictionary<string, object>? MessageParams = null);
