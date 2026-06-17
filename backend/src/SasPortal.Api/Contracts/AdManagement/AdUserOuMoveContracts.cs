namespace SasPortal.Api.Contracts.AdManagement;

public sealed class MoveAdUserOuRequest
{
    public string TargetOuDistinguishedName { get; init; } = string.Empty;
}

public sealed record MoveAdUserOuResponse(
    bool Success,
    string Message,
    string UserId,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DistinguishedName,
    string? PreviousDistinguishedName,
    string? TargetOuDistinguishedName,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
