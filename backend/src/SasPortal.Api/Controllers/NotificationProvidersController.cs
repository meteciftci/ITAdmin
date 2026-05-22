using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.NotificationProviders;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using AppModels = SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/notification-providers")]
[Authorize]
public sealed class NotificationProvidersController(INotificationProviderSettingsService settingsService) : ControllerBase
{
    [HttpGet("sms")]
    [RequirePermission(NotificationProviderPermissions.View)]
    public async Task<ActionResult<SmsProviderSettingsResponse>> GetSmsSettings(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSmsSettingsAsync(cancellationToken);
        return Ok(MapSms(settings));
    }

    [HttpGet("email")]
    [RequirePermission(NotificationProviderPermissions.View)]
    public async Task<ActionResult<EmailProviderSettingsResponse>> GetEmailSettings(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEmailSettingsAsync(cancellationToken);
        return Ok(MapEmail(settings));
    }

    [HttpPut("sms")]
    [RequirePermission(NotificationProviderPermissions.Update)]
    public async Task<ActionResult<SmsProviderSettingsResponse>> UpdateSmsSettings(
        [FromBody] UpdateSmsProviderSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateSmsSettingsAsync(
            new AppModels.UpdateSmsProviderSettingsRequest(
                request.IsEnabled,
                request.DisplayName,
                request.Sender,
                request.TimeoutSeconds,
                request.EndpointUrl,
                request.Method,
                request.ContentType,
                request.AuthType,
                request.ApiKeyName,
                request.BasicUserName,
                request.BasicPassword,
                request.BearerToken,
                request.ApiKeyValue,
                MapPairs(request.Headers),
                MapPairs(request.QueryParameters),
                request.BodyTemplate,
                request.SuccessStatusCodes,
                request.SuccessBodyContains,
                request.TurkishCharacterMode,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.SmsSettings is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapSms(result.SmsSettings));
    }

    [HttpPut("email")]
    [RequirePermission(NotificationProviderPermissions.Update)]
    public async Task<ActionResult<EmailProviderSettingsResponse>> UpdateEmailSettings(
        [FromBody] UpdateEmailProviderSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateEmailSettingsAsync(
            new AppModels.UpdateEmailProviderSettingsRequest(
                request.IsEnabled,
                request.DisplayName,
                request.Host,
                request.Port,
                request.UseSsl,
                request.UserName,
                request.Password,
                request.FromAddress,
                request.FromDisplayName,
                request.TimeoutSeconds,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.EmailSettings is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapEmail(result.EmailSettings));
    }

    [HttpPost("sms/test")]
    [RequirePermission(NotificationProviderPermissions.Test)]
    public async Task<ActionResult<NotificationProviderOperationResponse>> TestSms(
        [FromBody] TestSmsProviderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.TestSmsAsync(
            new AppModels.TestSmsProviderRequest(
                request.PhoneNumber,
                request.Message,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new NotificationProviderOperationResponse(result.Message, ProviderSummary: result.ProviderSummary));
        }

        return Ok(new NotificationProviderOperationResponse(result.Message, ProviderSummary: result.ProviderSummary));
    }

    [HttpPost("email/test")]
    [RequirePermission(NotificationProviderPermissions.Test)]
    public async Task<ActionResult<NotificationProviderOperationResponse>> TestEmail(
        [FromBody] TestEmailProviderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.TestEmailAsync(
            new AppModels.TestEmailProviderRequest(
                request.RecipientEmail,
                request.Subject,
                request.Body,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new NotificationProviderOperationResponse(
                result.Message,
                EmailSettings: result.EmailSettings is null ? null : MapEmail(result.EmailSettings),
                ProviderSummary: result.ProviderSummary));
        }

        return Ok(new NotificationProviderOperationResponse(
            result.Message,
            EmailSettings: result.EmailSettings is null ? null : MapEmail(result.EmailSettings),
            ProviderSummary: result.ProviderSummary));
    }

    private static IReadOnlyList<AppModels.NotificationKeyValuePair> MapPairs(
        IReadOnlyList<NotificationKeyValuePairRequest> pairs) =>
        pairs.Select(x => new AppModels.NotificationKeyValuePair(x.Key, x.Value)).ToList();

    private static SmsProviderSettingsResponse MapSms(AppModels.SmsProviderSettingsResponse settings) =>
        new(
            settings.Channel,
            settings.ProviderKey,
            settings.IsEnabled,
            settings.DisplayName,
            settings.Sender,
            settings.TimeoutSeconds,
            settings.EndpointUrl,
            settings.Method,
            settings.ContentType,
            settings.AuthType,
            settings.ApiKeyName,
            settings.Headers.Select(x => new NotificationKeyValuePairResponse(x.Key, x.Value)).ToList(),
            settings.QueryParameters.Select(x => new NotificationKeyValuePairResponse(x.Key, x.Value)).ToList(),
            settings.BodyTemplate,
            settings.SuccessStatusCodes,
            settings.SuccessBodyContains,
            settings.TurkishCharacterMode,
            settings.HasBasicPassword,
            settings.HasBearerToken,
            settings.HasApiKey,
            settings.LastValidatedAt,
            settings.LastValidationStatus,
            settings.LastValidationMessage);

    private static EmailProviderSettingsResponse MapEmail(AppModels.EmailProviderSettingsResponse settings) =>
        new(
            settings.Channel,
            settings.ProviderKey,
            settings.IsEnabled,
            settings.DisplayName,
            settings.Host,
            settings.Port,
            settings.UseSsl,
            settings.UserName,
            settings.FromAddress,
            settings.FromDisplayName,
            settings.TimeoutSeconds,
            settings.HasPassword,
            settings.LastValidatedAt,
            settings.LastValidationStatus,
            settings.LastValidationMessage);

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
