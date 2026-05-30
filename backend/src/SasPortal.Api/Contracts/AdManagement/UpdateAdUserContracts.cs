namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdUserRequest
{
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string SamAccountName { get; init; } = string.Empty;
    public string UserPrincipalName { get; init; } = string.Empty;
    public string? Mail { get; init; }
    public string? Department { get; init; }
    public IReadOnlyList<UpdateAdUserMappedAttributeRequest> MappedAttributes { get; init; } = [];
}

public sealed record UpdateAdUserMappedAttributeRequest
{
    public string LogicalField { get; init; } = string.Empty;
    public object? Value { get; init; }
}
