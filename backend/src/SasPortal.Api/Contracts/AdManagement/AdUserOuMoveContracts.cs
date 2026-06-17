namespace SasPortal.Api.Contracts.AdManagement;

public sealed class MoveAdUserOuRequest
{
    public string TargetOuDistinguishedName { get; init; } = string.Empty;
}

public sealed record MoveAdUserOuResponse(
    bool Success,
    string MessageKey,
    string UserId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DistinguishedName,
    string? PreviousDistinguishedName,
    string? TargetOuDistinguishedName,
    IReadOnlyDictionary<string, object>? MessageParams = null);
