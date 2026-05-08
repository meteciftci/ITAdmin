namespace SasPortal.Application.Common.Models;

public sealed record SettingsOverview(
    LdapSettingsModel? Ldap,
    IReadOnlyList<ApplicationSettingItem> ApplicationSettings,
    BrandingSettings Branding);
