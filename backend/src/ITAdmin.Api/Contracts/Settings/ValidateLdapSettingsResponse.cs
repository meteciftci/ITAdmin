namespace ITAdmin.Api.Contracts.Settings;

public sealed record ValidateLdapSettingsResponse(
    bool IsValid,
    string Message,
    IReadOnlyList<LdapConnectionDiagnosticDetailResponse> Details);
