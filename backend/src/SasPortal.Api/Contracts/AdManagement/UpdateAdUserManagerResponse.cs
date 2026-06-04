namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdUserManagerResponse(
    bool Success,
    string Message,
    string UserId,
    string? SamAccountName,
    string? ManagerDistinguishedName,
    string? ManagerDisplayName);
