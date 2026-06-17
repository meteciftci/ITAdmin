namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdUserManagerResponse(
    bool Success,
    string Message,
    string UserId,
    string? SamAccountName,
    string? ManagerDistinguishedName,
    string? ManagerDisplayName,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
