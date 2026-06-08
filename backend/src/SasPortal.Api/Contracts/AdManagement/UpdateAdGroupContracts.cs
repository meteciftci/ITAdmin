namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdGroupRequest(
    string DisplayName,
    string Name,
    string SamAccountName,
    string? Description);
