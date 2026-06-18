namespace ITAdmin.Api.Contracts.AdManagement;

public sealed record UpdateAdUserAccountExpirationResponse(
    bool Success,
    string MessageKey,
    string UserId,
    string? SamAccountName,
    string? AccountExpiresDate,
    bool NeverExpires,
    IReadOnlyDictionary<string, object>? MessageParams = null);
