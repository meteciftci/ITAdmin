using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.AdManagement;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/ad-management")]
[Authorize]
public sealed class AdManagementController(
    IAdManagementSettingsService settingsService,
    IAdAttributeMappingService attributeMappingService,
    IAdManagementValidationService validationService) : ControllerBase
{
    [HttpGet("settings")]
    [RequirePermission(AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<AdManagementSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        return Ok(MapSettings(settings));
    }

    [HttpPut("settings")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdManagementSettingsResponse>> UpdateSettings(
        [FromBody] AdManagementSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateSettingsAsync(
            new AppModels.UpdateAdManagementSettingsRequest(
                request.IsEnabled,
                request.DomainFqdn,
                request.NetbiosDomainName,
                request.DefaultNamingContext,
                request.BaseDn,
                request.UsersRootOu,
                request.DisabledUsersOu,
                request.GroupsSearchBase,
                request.ComputersSearchBase,
                request.PreferredDomainControllers,
                request.UseSsl,
                request.LdapPort,
                request.ServiceAccountUserName,
                request.ServiceAccountPassword,
                request.ClearServiceAccountPassword,
                request.PowerShellHealthEnabled,
                request.PowerShellTimeoutSeconds,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Settings is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapSettings(result.Settings));
    }

    [HttpPost("settings/validate")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdManagementValidationResponse>> ValidateSettings(
        CancellationToken cancellationToken)
    {
        var validationRequest = new AppModels.AdManagementValidationRequest(
            ResolveActorUserId(User),
            ResolveActorUserName(User),
            ResolveIpAddress(),
            ResolveUserAgent());

        var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);
        var primaryDc = ResolvePrimaryDomainController(connection);

        var result = await validationService.ValidateAsync(validationRequest, cancellationToken);

        await settingsService.RecordValidationResultAsync(
            result,
            validationRequest,
            primaryDc,
            cancellationToken);

        return Ok(MapValidation(result));
    }

    private static string? ResolvePrimaryDomainController(
        AppModels.AdManagementConnectionParameters? connection)
    {
        if (connection is null)
        {
            return null;
        }

        if (connection.PreferredDomainControllers.Count > 0)
        {
            var first = connection.PreferredDomainControllers[0];
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return string.IsNullOrWhiteSpace(connection.DomainFqdn) ? null : connection.DomainFqdn;
    }

    [HttpGet("attribute-mappings")]
    [RequirePermission(AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<IReadOnlyList<AdAttributeMappingResponse>>> GetMappings(
        CancellationToken cancellationToken)
    {
        var items = await attributeMappingService.GetMappingsAsync(cancellationToken);
        return Ok(items.Select(MapMapping).ToList());
    }

    [HttpPost("attribute-mappings")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdAttributeMappingResponse>> CreateMapping(
        [FromBody] CreateAdAttributeMappingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.CreateAsync(
            new AppModels.CreateAdAttributeMappingRequest(
                request.LogicalField,
                request.DisplayName,
                request.AttributeName,
                request.IsEnabled,
                request.IsEditable,
                request.IsSensitive,
                request.ValidationType,
                request.MaskingStrategy,
                request.SortOrder,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Mapping is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapMapping(result.Mapping));
    }

    [HttpPut("attribute-mappings/{id:guid}")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdAttributeMappingResponse>> UpdateMapping(
        [FromRoute] Guid id,
        [FromBody] UpdateAdAttributeMappingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.UpdateAsync(
            new AppModels.UpdateAdAttributeMappingRequest(
                id,
                request.DisplayName,
                request.AttributeName,
                request.IsEnabled,
                request.IsEditable,
                request.IsSensitive,
                request.ValidationType,
                request.MaskingStrategy,
                request.SortOrder,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Mapping is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapMapping(result.Mapping));
    }

    [HttpDelete("attribute-mappings/{id:guid}")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult> DeleteMapping(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.DeleteAsync(
            new AppModels.DeleteAdAttributeMappingRequest(
                id,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return NoContent();
    }

    private static AdManagementSettingsResponse MapSettings(AppModels.AdManagementSettingsModel settings) =>
        new(
            settings.IsEnabled,
            settings.DomainFqdn,
            settings.NetbiosDomainName,
            settings.DefaultNamingContext,
            settings.BaseDn,
            settings.UsersRootOu,
            settings.DisabledUsersOu,
            settings.GroupsSearchBase,
            settings.ComputersSearchBase,
            settings.PreferredDomainControllers,
            settings.UseSsl,
            settings.LdapPort,
            settings.ServiceAccountUserName,
            settings.HasServiceAccountPassword,
            settings.PowerShellHealthEnabled,
            settings.PowerShellTimeoutSeconds,
            settings.LastValidatedAt,
            settings.LastValidationStatus,
            settings.LastValidationMessage);

    private static AdAttributeMappingResponse MapMapping(AppModels.AdAttributeMappingItem item) =>
        new(
            item.Id,
            item.LogicalField,
            item.DisplayName,
            item.AttributeName,
            item.IsEnabled,
            item.IsEditable,
            item.IsSensitive,
            item.ValidationType,
            item.MaskingStrategy,
            item.SortOrder);

    private static AdManagementValidationResponse MapValidation(AppModels.AdManagementValidationResult result) =>
        new(
            result.IsValid,
            result.Message,
            result.CheckedAt,
            result.Details
                .Select(d => new AdManagementValidationDetailResponse(d.Key, d.Status, d.Message))
                .ToList());

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
            ?? principal.FindFirst("sub")?.Value;
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
}
