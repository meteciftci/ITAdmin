namespace SasPortal.Api.Contracts.Settings;

public sealed record SettingsOverviewResponse(
    LdapSettingsResponse? Ldap,
    IReadOnlyList<ApplicationSettingResponse> ApplicationSettings,
    BrandingSettingsResponse Branding,
    SessionSecuritySettingsResponse SessionSecurity);
