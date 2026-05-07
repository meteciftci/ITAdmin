using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.Settings;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Domain.Enums;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public sealed class SettingsController(ISettingsService settingsService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Settings.View")]
    public async Task<ActionResult<SettingsOverviewResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        return Ok(MapSettingsOverview(settings));
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

    private static SettingsOverviewResponse MapSettingsOverview(AppModels.SettingsOverview settings) =>
        new(
            settings.Ldap is null ? null : MapLdapSettings(settings.Ldap),
            settings.ApplicationSettings
                .Select(MapApplicationSetting)
                .ToList());

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
