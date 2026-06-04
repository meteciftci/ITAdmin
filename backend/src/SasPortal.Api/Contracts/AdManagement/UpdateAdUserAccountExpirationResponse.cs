namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdUserAccountExpirationResponse(
    bool Success,
    string Message,
    string UserId,
    string? SamAccountName,
    DateTimeOffset? AccountExpiresAt,
    bool NeverExpires);
