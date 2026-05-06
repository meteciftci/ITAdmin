using Microsoft.AspNetCore.Authorization;
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
    [RequirePermission("AuditLogs.View")]
    public async Task<ActionResult<PagedResponse<AuditLogListItemResponse>>> GetAuditLogs(
        [FromQuery] string? search,
        [FromQuery] string? action,
        [FromQuery] string? entityName,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await auditLogService.GetAuditLogsAsync(
            new AppModels.AuditLogListQuery(search, action, entityName, actorUserId, from, to, pageNumber, pageSize),
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
}
