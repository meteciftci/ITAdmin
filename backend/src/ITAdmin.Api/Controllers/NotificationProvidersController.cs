using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.NotificationProviders;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using AppModels = ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Api.Controllers;

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
            new AppModels.UpdateSmsProviderSettingsRequest
            {
                ProviderKey = request.ProviderKey,
                IsEnabled = request.IsEnabled,
                DisplayName = request.DisplayName,
                Sender = request.Sender,
                TimeoutSeconds = request.TimeoutSeconds,
                TurkishCharacterMode = request.TurkishCharacterMode,
                EndpointUrl = request.EndpointUrl,
                Method = request.Method,
                ContentType = request.ContentType,
                AuthType = request.AuthType,
                ApiKeyName = request.ApiKeyName,
                BasicUserName = request.BasicUserName,
                BasicPassword = request.BasicPassword,
                BearerToken = request.BearerToken,
                ApiKeyValue = request.ApiKeyValue,
                Headers = MapPairs(request.Headers),
                QueryParameters = MapPairs(request.QueryParameters),
                BodyTemplate = request.BodyTemplate,
                SuccessStatusCodes = request.SuccessStatusCodes,
                SuccessBodyContains = request.SuccessBodyContains,
                TeknomartBaseUrl = request.TeknomartBaseUrl,
                TeknomartDefaultSmsKind = request.TeknomartDefaultSmsKind,
                TeknomartSingleSmsTitle = request.TeknomartSingleSmsTitle,
                TeknomartEncoding = request.TeknomartEncoding,
                TeknomartValidity = request.TeknomartValidity,
                TeknomartCommercial = request.TeknomartCommercial,
                TeknomartPushWebhookUrl = request.TeknomartPushWebhookUrl,
                TeknomartUsername = request.TeknomartUsername,
                TeknomartPassword = request.TeknomartPassword,
                ActorUserId = ResolveActorUserId(User),
                ActorUserName = ResolveActorUserName(User),
                ActorIpAddress = ResolveIpAddress(),
                ActorUserAgent = ResolveUserAgent(),
            },
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
        new()
        {
            Channel = settings.Channel,
            ProviderKey = settings.ProviderKey,
            AvailableProviders = settings.AvailableProviders,
            IsEnabled = settings.IsEnabled,
            DisplayName = settings.DisplayName,
            Sender = settings.Sender,
            TimeoutSeconds = settings.TimeoutSeconds,
            TurkishCharacterMode = settings.TurkishCharacterMode,
            EndpointUrl = settings.EndpointUrl,
            Method = settings.Method,
            ContentType = settings.ContentType,
            AuthType = settings.AuthType,
            ApiKeyName = settings.ApiKeyName,
            Headers = settings.Headers.Select(x => new NotificationKeyValuePairResponse(x.Key, x.Value)).ToList(),
            QueryParameters = settings.QueryParameters.Select(x => new NotificationKeyValuePairResponse(x.Key, x.Value)).ToList(),
            BodyTemplate = settings.BodyTemplate,
            SuccessStatusCodes = settings.SuccessStatusCodes,
            SuccessBodyContains = settings.SuccessBodyContains,
            HasBasicPassword = settings.HasBasicPassword,
            HasBearerToken = settings.HasBearerToken,
            HasApiKey = settings.HasApiKey,
            TeknomartBaseUrl = settings.TeknomartBaseUrl,
            TeknomartDefaultSmsKind = settings.TeknomartDefaultSmsKind,
            TeknomartSingleSmsTitle = settings.TeknomartSingleSmsTitle,
            TeknomartEncoding = settings.TeknomartEncoding,
            TeknomartValidity = settings.TeknomartValidity,
            TeknomartCommercial = settings.TeknomartCommercial,
            TeknomartPushWebhookUrl = settings.TeknomartPushWebhookUrl,
            HasTeknomartCredentials = settings.HasTeknomartCredentials,
            LastValidatedAt = settings.LastValidatedAt,
            LastValidationStatus = settings.LastValidationStatus,
            LastValidationMessage = settings.LastValidationMessage,
        };

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
