using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.Common;
using ITAdmin.Api.Contracts.DnsManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppModels = ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/dns-management/operation-logs")]
[Authorize]
public sealed class DnsOperationLogsController(IDnsOperationLogService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(DnsManagementPermissions.ViewOperationLogs)]
    public async Task<ActionResult<PagedResponse<DnsOperationLogListItemResponse>>> Get(
        [FromQuery] Guid? serverId,
        [FromQuery] string? operationType,
        [FromQuery] string? status,
        [FromQuery] string? targetSearch,
        [FromQuery] string? actorUserName,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await service.GetAsync(new(
            serverId, operationType, status, targetSearch, actorUserName,
            dateFrom, dateTo, pageNumber, pageSize), cancellationToken);
        return Ok(new PagedResponse<DnsOperationLogListItemResponse>(
            result.Items.Select(Map).ToList(), result.PageNumber, result.PageSize,
            result.TotalCount, result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(DnsManagementPermissions.ViewOperationLogs)]
    public async Task<ActionResult<DnsOperationLogDetailResponse>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(Map(result));
    }

    private static DnsOperationLogListItemResponse Map(AppModels.DnsOperationLogListItemModel x) => new(
        x.Id, x.CreatedAt, x.ServerId, x.ServerDisplayName, x.OperationType, x.Status,
        x.ZoneName, x.RecordName, x.RecordType, x.ActorUserName, x.ErrorCode, x.ErrorMessage,
        x.HasRequestSummary, x.HasBeforeSnapshot, x.HasAfterSnapshot);

    private static DnsOperationLogDetailResponse Map(AppModels.DnsOperationLogDetailModel x) => new(
        x.Id, x.CreatedAt, x.ServerId, x.ServerDisplayName, x.OperationType, x.Status,
        x.ZoneName, x.RecordName, x.RecordType, x.RequestSummaryJson, x.BeforeSnapshotJson,
        x.AfterSnapshotJson, x.ErrorCode, x.ErrorMessage, x.ActorUserId, x.ActorUserName,
        x.IpAddress, x.UserAgent, x.CorrelationId);
}
