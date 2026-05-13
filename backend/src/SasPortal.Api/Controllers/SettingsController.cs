using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.Settings;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Domain.Enums;
using SixLabors.ImageSharp;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public sealed class SettingsController(
    ISettingsService settingsService,
    IWebHostEnvironment webHostEnvironment) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Settings.View")]
    public async Task<ActionResult<SettingsOverviewResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        return Ok(MapSettingsOverview(settings));
    }

    [HttpGet("branding")]
    [AllowAnonymous]
    public async Task<ActionResult<BrandingSettingsResponse>> GetBrandingSettings(CancellationToken cancellationToken)
    {
        var branding = await settingsService.GetBrandingSettingsAsync(cancellationToken);
        return Ok(MapBranding(branding));
    }

    [HttpPut("ldap")]
    [RequirePermission("Settings.Update")]
    public async Task<ActionResult<LdapSettingsResponse>> UpdateLdapSettings(
        [FromBody] UpdateLdapSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateLdapSettingsAsync(
            new AppModels.UpdateLdapSettingsRequest(
                request.Name,
                request.Host,
                request.Port,
                request.UseSsl,
                request.BaseDn,
                request.UserSearchBase,
                request.UserSearchFilter,
                request.BindUserName,
                request.BindUserDomain,
                request.BindPassword,
                request.Description,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        if (result.Settings?.Ldap is null)
        {
            return BadRequest(new { message = "LDAP settings could not be loaded." });
        }

        return Ok(MapLdapSettings(result.Settings.Ldap));
    }

    [HttpPost("ldap/validate")]
    [RequirePermission("Settings.Update")]
    public async Task<ActionResult<ValidateLdapSettingsResponse>> ValidateLdapSettings(
        [FromBody] ValidateLdapSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.ValidateLdapSettingsAsync(
            new AppModels.ValidateLdapSettingsRequest(
                request.Name,
                request.Host,
                request.Port,
                request.UseSsl,
                request.BaseDn,
                request.UserSearchBase,
                request.UserSearchFilter,
                request.BindUserName,
                request.BindUserDomain,
                request.BindPassword,
                request.TestUserName,
                request.TestPassword,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        return Ok(new ValidateLdapSettingsResponse(result.IsValid, result.Message));
    }

    [HttpPut("application")]
    [RequirePermission("Settings.Update")]
    public async Task<ActionResult<SettingsOverviewResponse>> UpdateApplicationSettings(
        [FromBody] UpdateApplicationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var items = new List<AppModels.UpdateApplicationSettingRequest>(request.Items.Count);
        foreach (var item in request.Items)
        {
            if (!Enum.IsDefined(typeof(SettingValueType), item.ValueType))
            {
                return BadRequest(new { message = $"Invalid setting value type for key: {item.Key}." });
            }

            items.Add(new AppModels.UpdateApplicationSettingRequest(
                item.Key,
                item.Value,
                (SettingValueType)item.ValueType));
        }

        var result = await settingsService.UpdateApplicationSettingsAsync(
            new AppModels.UpdateApplicationSettingsRequest(
                items,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Settings is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapSettingsOverview(result.Settings));
    }

    [HttpPut("session-security")]
    [RequirePermission("Settings.Update")]
    public async Task<ActionResult<SettingsOverviewResponse>> UpdateSessionSecuritySettings(
        [FromBody] UpdateSessionSecuritySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateSessionSecuritySettingsAsync(
            new AppModels.UpdateSessionSecuritySettingsRequest(
                request.AccessTokenMinutes,
                request.IdleTimeoutMinutes,
                request.IdleWarningSeconds,
                request.SessionRefreshTokenHours,
                request.RememberMeRefreshTokenDays,
                request.RememberMeEnabled,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Settings is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapSettingsOverview(result.Settings));
    }

    [HttpPost("branding/logo")]
    [RequirePermission("Settings.Update")]
    public async Task<ActionResult<BrandingLogoUploadResponse>> UploadBrandingLogo(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Logo file is required." });
        }

        const long maxFileSizeInBytes = 2 * 1024 * 1024;
        if (file.Length > maxFileSizeInBytes)
        {
            return BadRequest(new { message = "Logo file size must be 2 MB or smaller." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!IsAllowedLogoExtension(extension))
        {
            return BadRequest(new { message = "Logo file extension must be .png, .jpg or .jpeg." });
        }

        var contentType = file.ContentType?.Trim() ?? string.Empty;
        if (!IsAllowedLogoContentType(contentType))
        {
            return BadRequest(new { message = "Logo content type must be image/png or image/jpeg." });
        }

        byte[] content;
        await using (var stream = file.OpenReadStream())
        await using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream, cancellationToken);
            content = memoryStream.ToArray();
        }

        if (!HasAllowedMagicBytes(content, extension))
        {
            return BadRequest(new { message = "Logo file signature is invalid." });
        }

        if (!ValidateImageDimensions(content, out var dimensionValidationMessage))
        {
            return BadRequest(new { message = dimensionValidationMessage });
        }

        var result = await settingsService.UploadBrandingLogoAsync(
            new AppModels.UploadBrandingLogoRequest(
                content,
                extension,
                contentType,
                ResolveBrandingUploadsDirectory(webHostEnvironment.WebRootPath),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        return Ok(new BrandingLogoUploadResponse(result.LogoUrl));
    }

    [HttpPost("branding/favicon")]
    [RequirePermission("Settings.Update")]
    public async Task<ActionResult<BrandingFaviconUploadResponse>> UploadBrandingFavicon(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Favicon file is required." });
        }

        const long maxFileSizeInBytes = 512 * 1024;
        if (file.Length > maxFileSizeInBytes)
        {
            return BadRequest(new { message = "Favicon file size must be 512 KB or smaller." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!IsAllowedLogoExtension(extension))
        {
            return BadRequest(new { message = "Favicon file extension must be .png, .jpg or .jpeg." });
        }

        var contentType = file.ContentType?.Trim() ?? string.Empty;
        if (!IsAllowedLogoContentType(contentType))
        {
            return BadRequest(new { message = "Favicon content type must be image/png or image/jpeg." });
        }

        byte[] content;
        await using (var stream = file.OpenReadStream())
        await using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream, cancellationToken);
            content = memoryStream.ToArray();
        }

        if (!HasAllowedMagicBytes(content, extension))
        {
            return BadRequest(new { message = "Favicon file signature is invalid." });
        }

        if (!ValidateFaviconDimensions(content, out var dimensionValidationMessage))
        {
            return BadRequest(new { message = dimensionValidationMessage });
        }

        var result = await settingsService.UploadBrandingFaviconAsync(
            new AppModels.UploadBrandingFaviconRequest(
                content,
                extension,
                contentType,
                ResolveBrandingUploadsDirectory(webHostEnvironment.WebRootPath),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        return Ok(new BrandingFaviconUploadResponse(result.FaviconUrl));
    }

    private static SettingsOverviewResponse MapSettingsOverview(AppModels.SettingsOverview settings) =>
        new(
            settings.Ldap is null ? null : MapLdapSettings(settings.Ldap),
            settings.ApplicationSettings
                .Select(MapApplicationSetting)
                .ToList(),
            MapBranding(settings.Branding),
            MapSessionSecurity(settings.SessionSecurity));

    private static SessionSecuritySettingsResponse MapSessionSecurity(AppModels.SessionSecuritySettings security) =>
        new(
            security.AccessTokenMinutes,
            security.IdleTimeoutMinutes,
            security.IdleWarningSeconds,
            security.SessionRefreshTokenHours,
            security.RememberMeRefreshTokenDays,
            security.RememberMeEnabled);

    private static LdapSettingsResponse MapLdapSettings(AppModels.LdapSettingsModel ldap) =>
        new(
            ldap.Id,
            ldap.Name,
            ldap.Host,
            ldap.Port,
            ldap.UseSsl,
            ldap.BaseDn,
            ldap.UserSearchBase,
            ldap.UserSearchFilter,
            ldap.BindUserName,
            ldap.BindUserDomain,
            ldap.HasBindPassword,
            ldap.Description,
            ldap.IsActive);

    private static ApplicationSettingResponse MapApplicationSetting(AppModels.ApplicationSettingItem item) =>
        new(
            item.Key,
            item.Value,
            (int)item.ValueType,
            item.Description,
            item.IsEncrypted,
            item.IsSystem,
            item.IsActive);

    private static BrandingSettingsResponse MapBranding(AppModels.BrandingSettings branding) =>
        new(
            branding.ApplicationName,
            branding.BrowserTitle,
            branding.LogoUrl,
            branding.FaviconUrl,
            branding.ForgotPasswordUrl);

    private static bool IsAllowedLogoExtension(string extension) =>
        extension is ".png" or ".jpg" or ".jpeg";

    private static bool IsAllowedLogoContentType(string contentType) =>
        string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase);

    private static bool HasAllowedMagicBytes(byte[] content, string extension)
    {
        if (content.Length < 4)
        {
            return false;
        }

        return extension switch
        {
            ".png" => content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47,
            ".jpg" or ".jpeg" => content[0] == 0xFF && content[1] == 0xD8,
            _ => false
        };
    }

    private static bool ValidateImageDimensions(byte[] content, out string message)
    {
        try
        {
            var image = Image.Identify(content);
            if (image is null)
            {
                message = "Logo image could not be read.";
                return false;
            }

            if (image.Width < 32 || image.Height < 32 || image.Width > 512 || image.Height > 512)
            {
                message = "Logo dimensions must be between 32x32 and 512x512 pixels.";
                return false;
            }

            message = string.Empty;
            return true;
        }
        catch
        {
            message = "Logo image could not be validated.";
            return false;
        }
    }

    private static bool ValidateFaviconDimensions(byte[] content, out string message)
    {
        try
        {
            var image = Image.Identify(content);
            if (image is null)
            {
                message = "Favicon image could not be read.";
                return false;
            }

            if (image.Width < 16 || image.Height < 16 || image.Width > 512 || image.Height > 512)
            {
                message = "Favicon dimensions must be between 16x16 and 512x512 pixels.";
                return false;
            }

            message = string.Empty;
            return true;
        }
        catch
        {
            message = "Favicon image could not be validated.";
            return false;
        }
    }

    private static string ResolveBrandingUploadsDirectory(string? webRootPath)
    {
        var root = string.IsNullOrWhiteSpace(webRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : webRootPath;

        return Path.Combine(root, "uploads", "branding");
    }

    private static string? ResolveActorUserName(ClaimsPrincipal principal)
    {
        if (!string.IsNullOrWhiteSpace(principal.Identity?.Name))
        {
            return principal.Identity!.Name;
        }

        var nameClaim = principal.FindFirst(ClaimTypes.Name) ?? principal.FindFirst("name");
        return string.IsNullOrWhiteSpace(nameClaim?.Value) ? null : nameClaim.Value.Trim();
    }

    private static Guid? ResolveActorUserId(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtSubClaimType)?.Value;

        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private string? ResolveIpAddress()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? null : ip;
    }

    private string? ResolveUserAgent()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }

    private const string JwtSubClaimType = "sub";
}
