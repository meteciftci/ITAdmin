namespace ITAdmin.Application.Common.AdManagement;

public sealed record AdUserManagerSnapshotInfo(
    string? Id,
    string? SamAccountName,
    string? UserPrincipalName,
    string? DisplayName,
    string? DistinguishedName);
