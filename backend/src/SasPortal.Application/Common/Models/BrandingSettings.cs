namespace SasPortal.Application.Common.Models;

public sealed record BrandingSettings(
    string ApplicationName,
    string BrowserTitle,
    string? LogoUrl,
    string? FaviconUrl,
    string? ForgotPasswordUrl);
