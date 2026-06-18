namespace ITAdmin.Api.Contracts.Setup;

public sealed record SearchSetupOrganizationalUnitsRequest(
    string SetupKey,
    CompleteSetupLdapSettingsRequest? Ldap,
    string? Search,
    string? ParentDistinguishedName);

public sealed record SearchSetupOrganizationalUnitsResponse(
    IReadOnlyList<SetupOrganizationalUnitListItemResponse> Items,
    bool HasMore);

public sealed record SetupOrganizationalUnitListItemResponse(
    string DistinguishedName,
    string? Name,
    string? DisplayName,
    string? Ou,
    string Label);
