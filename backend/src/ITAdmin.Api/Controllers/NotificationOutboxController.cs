using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.Common;
using ITAdmin.Api.Contracts.NotificationOutbox;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using AppModels = ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/notification-outbox")]
[Authorize]
public sealed class NotificationOutboxController(INotificationOutboxService outboxService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(NotificationOutboxPermissions.View)]
    public async Task<ActionResult<PagedResponse<NotificationOutboxListItemResponse>>> GetList(
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] string? relatedModule,
        [FromQuery] string? relatedEvent,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await outboxService.GetListAsync(
            new AppModels.NotificationOutboxListQuery(
                channel,
                status,
                relatedModule,
                relatedEvent,
                search,
                pageNumber,
                pageSize),
            cancellationToken);

        return Ok(new PagedResponse<NotificationOutboxListItemResponse>(
            result.Items.Select(MapListItem).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(NotificationOutboxPermissions.View)]
    public async Task<ActionResult<NotificationOutboxDetailResponse>> GetDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await outboxService.GetDetailAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new { message = "Notification outbox item was not found." });
        }

        return Ok(MapDetail(item));
    }

    [HttpPost("{id:guid}/retry")]
    [RequirePermission(NotificationOutboxPermissions.Retry)]
    public async Task<ActionResult<NotificationOutboxDetailResponse>> Retry(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await outboxService.RetryAsync(id, ResolveActor(), cancellationToken);
        if (!result.IsSuccess || result.Item is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Item));
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(NotificationOutboxPermissions.Cancel)]
    public async Task<ActionResult<NotificationOutboxDetailResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await outboxService.CancelAsync(id, ResolveActor(), cancellationToken);
        if (!result.IsSuccess || result.Item is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Item));
    }

    private static NotificationOutboxListItemResponse MapListItem(AppModels.NotificationOutboxListItem item) =>
        new(
            item.Id,
            item.Channel,
            item.ProviderKey,
            item.RecipientMasked,
            item.Subject,
            item.Status,
            item.AttemptCount,
            item.MaxAttempts,
            item.NextAttemptAt,
            item.LastAttemptAt,
            item.SentAt,
            item.RelatedModule,
            item.RelatedEvent,
            item.RelatedEntityType,
            item.RelatedEntityId,
            item.CreatedAt,
            item.ProviderSummary,
            item.LastErrorMessage);

    private static NotificationOutboxDetailResponse MapDetail(AppModels.NotificationOutboxDetail item) =>
        new(
            item.Id,
            item.Channel,
            item.ProviderKey,
            item.RecipientMasked,
            item.Subject,
            item.Body,
            item.Status,
            item.AttemptCount,
            item.MaxAttempts,
            item.NextAttemptAt,
            item.LastAttemptAt,
            item.SentAt,
            item.LockedAt,
            item.LockedBy,
            item.LastErrorMessage,
            item.ProviderSummary,
            item.RelatedModule,
            item.RelatedEvent,
            item.RelatedEntityType,
            item.RelatedEntityId,
            item.CorrelationId,
            item.CreatedAt,
            item.CreatedBy,
            item.UpdatedAt,
            item.UpdatedBy);

    private AppModels.NotificationOutboxActorRequest ResolveActor() =>
        new(
            ResolveActorUserId(User),
            ResolveActorUserName(User),
            ResolveIpAddress(),
            ResolveUserAgent());

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
