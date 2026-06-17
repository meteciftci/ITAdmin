namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdUserAccountOperationResponse(
    bool Success,
    string MessageKey,
    string UserId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DistinguishedName,
    bool? IsEnabled,
    bool? IsLockedOut,
    IReadOnlyDictionary<string, object>? MessageParams = null);
