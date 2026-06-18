namespace ITAdmin.Api.Contracts.Settings;

public sealed record BrandingSettingsResponse(
    string ApplicationName,
    string BrowserTitle,
    string? LogoUrl,
    string? FaviconUrl,
    string? ForgotPasswordUrl,
    string FooterText);
