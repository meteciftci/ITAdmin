namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdUserAccountExpirationResponse(
    bool Success,
    string Message,
    string UserId,
    string? SamAccountName,
    string? AccountExpiresDate,
    bool NeverExpires);
