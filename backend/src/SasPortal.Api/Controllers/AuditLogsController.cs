using Microsoft.AspNetCore.Authorization;
using SasPortal.Application.Common.Security;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.AuditLogs;
using SasPortal.Api.Contracts.Common;
using SasPortal.Application.Abstractions.Services;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public sealed class AuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.AuditLogs.View)]
    public async Task<ActionResult<PagedResponse<AuditLogListItemResponse>>> GetAuditLogs(
        [FromQuery] string? search,
        [FromQuery] string? action,
        [FromQuery] List<string>? actions,
        [FromQuery(Name = "actions[]")] List<string>? actionsBracket,
        [FromQuery] string? entityName,
        [FromQuery] List<string>? entityNames,
        [FromQuery(Name = "entityNames[]")] List<string>? entityNamesBracket,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var allActions = (actions ?? [])
            .Concat(actionsBracket ?? [])
            .ToList();
        var allEntityNames = (entityNames ?? [])
            .Concat(entityNamesBracket ?? [])
            .ToList();

        var result = await auditLogService.GetAuditLogsAsync(
            new AppModels.AuditLogListQuery(
                search,
                action,
                allActions,
                entityName,
                allEntityNames,
                actorUserId,
                from,
                to,
                pageNumber,
                pageSize),
            cancellationToken);

        var response = new PagedResponse<AuditLogListItemResponse>(
            result.Items.Select(x => new AuditLogListItemResponse(
                x.Id,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Description,
                x.ActorUserId,
                x.ActorUserName,
                x.IpAddress,
                x.UserAgent,
                x.CreatedAt)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    [HttpGet("filter-options")]
    [RequirePermission(PermissionCodes.AuditLogs.View)]
    public async Task<ActionResult<AuditLogFilterOptionsResponse>> GetFilterOptions(
        CancellationToken cancellationToken = default)
    {
        var result = await auditLogService.GetFilterOptionsAsync(cancellationToken);
        return Ok(new AuditLogFilterOptionsResponse(result.Actions, result.EntityNames));
    }
}
