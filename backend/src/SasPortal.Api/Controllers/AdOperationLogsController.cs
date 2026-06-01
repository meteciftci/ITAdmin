using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.AdManagement;
using SasPortal.Api.Contracts.Common;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/ad-management/operation-logs")]
[Authorize]
public sealed class AdOperationLogsController(IAdOperationLogService adOperationLogService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AdManagementPermissions.OperationLogsView)]
    public async Task<ActionResult<PagedResponse<AdOperationLogListItemResponse>>> GetOperationLogs(
        [FromQuery] string? operationType,
        [FromQuery] string? status,
        [FromQuery] string? targetObjectType,
        [FromQuery] string? targetSamAccountName,
        [FromQuery] string? targetObjectGuid,
        [FromQuery] string? actorUserName,
        [FromQuery] string? domainController,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await adOperationLogService.GetLogsAsync(
            new AppModels.AdOperationLogListQuery(
                operationType,
                status,
                targetObjectType,
                targetSamAccountName,
                targetObjectGuid,
                actorUserName,
                domainController,
                dateFrom,
                dateTo,
                pageNumber,
                pageSize),
            cancellationToken);

        var response = new PagedResponse<AdOperationLogListItemResponse>(
            result.Items.Select(MapListItem).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AdManagementPermissions.OperationLogsView)]
    public async Task<ActionResult<AdOperationLogDetailResponse>> GetOperationLogById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var detail = await adOperationLogService.GetLogByIdAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        return Ok(MapDetail(detail));
    }

    private static AdOperationLogListItemResponse MapListItem(AppModels.AdOperationLogListItem item) =>
        new(
            item.Id,
            item.CreatedAt,
            item.OperationType,
            item.Status,
            item.TargetObjectType,
            item.TargetObjectGuid,
            item.TargetDistinguishedName,
            item.TargetSamAccountName,
            item.ActorUserId,
            item.ActorUserName,
            item.IpAddress,
            item.DomainController,
            item.ErrorMessage,
            item.HasError,
            item.HasBeforeSnapshot,
            item.HasAfterSnapshot,
            item.HasRequestSummary);

    private static AdOperationLogDetailResponse MapDetail(AppModels.AdOperationLogDetail item) =>
        new(
            item.Id,
            item.CreatedAt,
            item.OperationType,
            item.Status,
            item.TargetObjectType,
            item.TargetObjectGuid,
            item.TargetDistinguishedName,
            item.TargetSamAccountName,
            item.ErrorCode,
            item.ErrorMessage,
            item.DomainController,
            item.RequestSummaryJson,
            item.BeforeSnapshotJson,
            item.AfterSnapshotJson,
            item.ActorUserId,
            item.ActorUserName,
            item.IpAddress,
            item.UserAgent,
            item.CorrelationId);
}
