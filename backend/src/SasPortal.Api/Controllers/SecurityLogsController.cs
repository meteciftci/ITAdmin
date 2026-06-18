using Microsoft.AspNetCore.Authorization;
using SasPortal.Application.Common.Security;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.Common;
using SasPortal.Api.Contracts.SecurityLogs;
using SasPortal.Application.Abstractions.Services;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/security-logs")]
[Authorize]
public sealed class SecurityLogsController(ISecurityLogService securityLogService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.SecurityLogs.View)]
    public async Task<ActionResult<PagedResponse<SecurityLogListItemResponse>>> GetSecurityLogs(
        [FromQuery] string? search,
        [FromQuery] List<string>? eventTypes,
        [FromQuery(Name = "eventTypes[]")] List<string>? eventTypesBracket,
        [FromQuery] List<string>? severities,
        [FromQuery(Name = "severities[]")] List<string>? severitiesBracket,
        [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var allEventTypes = (eventTypes ?? [])
            .Concat(eventTypesBracket ?? [])
            .ToList();
        var allSeverities = (severities ?? [])
            .Concat(severitiesBracket ?? [])
            .ToList();

        var result = await securityLogService.GetSecurityLogsAsync(
            new AppModels.SecurityLogListQuery(
                search,
                allEventTypes,
                allSeverities,
                userId,
                from,
                to,
                pageNumber,
                pageSize),
            cancellationToken);

        var response = new PagedResponse<SecurityLogListItemResponse>(
            result.Items.Select(x => new SecurityLogListItemResponse(
                x.Id,
                x.EventType,
                x.Severity,
                x.UserId,
                x.UserName,
                x.IpAddress,
                x.UserAgent,
                x.Description,
                x.CreatedAt)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

        return Ok(response);
    }

    [HttpGet("filter-options")]
    [RequirePermission(PermissionCodes.SecurityLogs.View)]
    public async Task<ActionResult<SecurityLogFilterOptionsResponse>> GetFilterOptions(
        CancellationToken cancellationToken = default)
    {
        var result = await securityLogService.GetFilterOptionsAsync(cancellationToken);
        return Ok(new SecurityLogFilterOptionsResponse(result.EventTypes, result.Severities));
    }
}
