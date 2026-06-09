namespace SasPortal.Api.Contracts.AdManagement;

public sealed class MoveAdGroupOuRequest
{
    public string TargetOuDistinguishedName { get; init; } = string.Empty;
}

public sealed record MoveAdGroupOuResponse(
    bool Success,
    string Message,
    string GroupId,
    string? DisplayName,
    string? Name,
    string? SamAccountName,
    string? DistinguishedName,
    string? PreviousDistinguishedName,
    string? TargetOuDistinguishedName);
