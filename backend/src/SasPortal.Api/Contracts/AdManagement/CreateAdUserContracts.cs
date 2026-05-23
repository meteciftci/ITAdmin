namespace SasPortal.Api.Contracts.AdManagement;

public sealed record CreateAdUserRequest
{
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string? Department { get; init; }
    public string? SamAccountName { get; init; }
    public string UpnSuffix { get; init; } = string.Empty;
    public string TargetOuDistinguishedName { get; init; } = string.Empty;
    public string InitialPassword { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public bool MustChangePasswordAtNextLogon { get; init; }
    public IReadOnlyList<CreateAdUserMappedAttributeRequest> MappedAttributes { get; init; } =
        Array.Empty<CreateAdUserMappedAttributeRequest>();
}

public sealed record CreateAdUserMappedAttributeRequest
{
    public string LogicalField { get; init; } = string.Empty;
    public object? Value { get; init; }
}

public sealed record CreateAdUserResponse(
    string Id,
    string DistinguishedName,
    string Cn,
    string SamAccountName,
    string UserPrincipalName,
    string DisplayName,
    bool IsEnabled,
    string Message,
    bool NamingCollisionResolved,
    int? GeneratedSuffix,
    AdUserCreatedNotificationSummaryResponse? NotificationSummary);

public sealed record AdOrganizationalUnitListItemResponse(
    string DistinguishedName,
    string? Name,
    string? DisplayName,
    string? Ou,
    string Label);

public sealed record AdOrganizationalUnitSearchResponse(
    IReadOnlyList<AdOrganizationalUnitListItemResponse> Items,
    bool HasMore);
