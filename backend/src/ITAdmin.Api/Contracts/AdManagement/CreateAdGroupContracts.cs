namespace ITAdmin.Api.Contracts.AdManagement;

public sealed record CreateAdGroupRequest(
    string DisplayName,
    string Name,
    string SamAccountName,
    string? Description,
    string GroupScope,
    string TargetOuDistinguishedName);
