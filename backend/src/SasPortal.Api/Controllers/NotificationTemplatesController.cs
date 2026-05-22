using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.NotificationTemplates;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using AppModels = SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/notification-templates")]
[Authorize]
public sealed class NotificationTemplatesController(
    INotificationTemplateService templateService,
    INotificationTemplateCatalogProvider catalogProvider) : ControllerBase
{
    [HttpGet("catalog")]
    [RequirePermission(NotificationTemplatePermissions.View)]
    public ActionResult<NotificationTemplateCatalogResponse> GetCatalog()
    {
        var catalog = catalogProvider.GetCatalog();
        return Ok(new NotificationTemplateCatalogResponse(
            catalog.Modules.Select(module => new NotificationTemplateCatalogModuleResponse(
                module.Key,
                module.Events.Select(evt => new NotificationTemplateCatalogEventResponse(
                    evt.Key,
                    evt.SupportedChannels,
                    evt.Variables.Select(variable => new NotificationTemplateCatalogVariableResponse(
                        variable.Key,
                        variable.Example)).ToList())).ToList())).ToList()));
    }

    [HttpGet]
    [RequirePermission(NotificationTemplatePermissions.View)]
    public async Task<ActionResult<IReadOnlyList<NotificationTemplateListItemResponse>>> GetList(
        [FromQuery] string? moduleKey,
        [FromQuery] string? eventKey,
        [FromQuery] string? channel,
        CancellationToken cancellationToken)
    {
        var items = await templateService.GetListAsync(
            new AppModels.NotificationTemplateListQuery(moduleKey, eventKey, channel),
            cancellationToken);

        return Ok(items.Select(MapListItem).ToList());
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(NotificationTemplatePermissions.View)]
    public async Task<ActionResult<NotificationTemplateResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var template = await templateService.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return NotFound(new { message = "Notification template was not found." });
        }

        return Ok(Map(template));
    }

    [HttpPost]
    [RequirePermission(NotificationTemplatePermissions.Update)]
    public async Task<ActionResult<NotificationTemplateResponse>> Create(
        [FromBody] SaveNotificationTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await templateService.CreateAsync(
            new AppModels.CreateNotificationTemplateRequest(
                request.ModuleKey,
                request.EventKey,
                request.Channel,
                request.Name,
                request.IsEnabled,
                request.SubjectTemplate,
                request.BodyTemplate,
                request.Description,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Template is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(Map(result.Template));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(NotificationTemplatePermissions.Update)]
    public async Task<ActionResult<NotificationTemplateResponse>> Update(
        Guid id,
        [FromBody] SaveNotificationTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await templateService.UpdateAsync(
            id,
            new AppModels.UpdateNotificationTemplateRequest(
                request.ModuleKey,
                request.EventKey,
                request.Channel,
                request.Name,
                request.IsEnabled,
                request.SubjectTemplate,
                request.BodyTemplate,
                request.Description,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Template is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(Map(result.Template));
    }

    private static NotificationTemplateListItemResponse MapListItem(AppModels.NotificationTemplateListItem item) =>
        new(item.Id, item.ModuleKey, item.EventKey, item.Channel, item.Name, item.IsEnabled, item.UpdatedAt);

    private static NotificationTemplateResponse Map(AppModels.NotificationTemplateModel template) =>
        new(
            template.Id,
            template.ModuleKey,
            template.EventKey,
            template.Channel,
            template.Name,
            template.IsEnabled,
            template.SubjectTemplate,
            template.BodyTemplate,
            template.Description,
            template.CreatedAt,
            template.CreatedBy,
            template.UpdatedAt,
            template.UpdatedBy);

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
