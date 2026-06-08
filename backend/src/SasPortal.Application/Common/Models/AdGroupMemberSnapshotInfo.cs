namespace SasPortal.Application.Common.Models;

public sealed record AdGroupMemberSnapshotInfo(
    string? Id,
    string Type,
    string? DisplayName,
    string? Name,
    string? Cn,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DNSHostName,
    string? Description,
    string DistinguishedName)
{
    public bool IsSecurityGroup { get; init; } = true;
    public bool? IsEnabled { get; init; }
}
