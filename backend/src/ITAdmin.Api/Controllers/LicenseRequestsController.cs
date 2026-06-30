using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.Common;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Enums;
using AppModels = ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/license-management/requests")]
[Authorize]
public sealed class LicenseRequestsController(ILicenseRequestService requestService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<PagedResponse<LicenseRequestListItemResponse>>> GetRequests(
        [FromQuery] string? search,
        [FromQuery] LicenseRequestStatus? status,
        [FromQuery] LicenseRequestSource? requestSource,
        [FromQuery] DateOnly? requestDateFrom,
        [FromQuery] DateOnly? requestDateTo,
        [FromQuery] string? requesterUnitObjectGuid,
        [FromQuery] Guid? productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await requestService.GetListAsync(
            new AppModels.LicenseRequestListQuery(
                search,
                status,
                requestSource,
                requestDateFrom,
                requestDateTo,
                requesterUnitObjectGuid,
                productId,
                pageNumber,
                pageSize),
            cancellationToken);

        return Ok(new PagedResponse<LicenseRequestListItemResponse>(
            result.Items.Select(x => new LicenseRequestListItemResponse(
                x.Id,
                x.RequestNumber,
                x.RequestSource,
                x.RequestDate,
                x.RequesterUnitDisplayName,
                x.RequesterManagerName,
                x.ProductCount,
                x.UserCount,
                x.EstimatedTotalCost,
                x.Currency,
                x.Status)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<LicenseRequestDetailResponse>> GetRequestById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var request = await requestService.GetByIdAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(new { message = "License request was not found." });
        }

        return Ok(MapDetail(request));
    }

    [HttpPost]
    [RequirePermission(LicenseManagementPermissions.ManageRequests)]
    public async Task<ActionResult<LicenseRequestDetailResponse>> CreateRequest(
        [FromBody] CreateLicenseRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasDirectoryLookupPermissions())
        {
            return Forbid();
        }

        var result = await requestService.CreateAsync(
            MapCreateRequest(request),
            cancellationToken);

        if (!result.IsSuccess || result.Request is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(
            nameof(GetRequestById),
            new { id = result.Request.Id },
            MapDetail(result.Request));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.ManageRequests)]
    public async Task<ActionResult<LicenseRequestDetailResponse>> UpdateRequest(
        Guid id,
        [FromBody] UpdateLicenseRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasDirectoryLookupPermissions())
        {
            return Forbid();
        }

        var result = await requestService.UpdateAsync(
            MapUpdateRequest(id, request),
            cancellationToken);

        if (!result.IsSuccess || result.Request is null)
        {
            return result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { message = result.Message })
                : BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Request));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(LicenseManagementPermissions.ManageRequests)]
    public async Task<ActionResult<LicenseRequestDetailResponse>> UpdateRequestStatus(
        Guid id,
        [FromBody] UpdateLicenseRequestStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await requestService.UpdateStatusAsync(
            new AppModels.UpdateLicenseRequestStatusRequest(
                id,
                request.Status,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Request is null)
        {
            return result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { message = result.Message })
                : BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Request));
    }

    private bool HasDirectoryLookupPermissions() =>
        (User.HasClaim(CustomClaimTypes.Permission, PermissionCodes.Directory.Users.Lookup)
         && User.HasClaim(CustomClaimTypes.Permission, PermissionCodes.Directory.OrganizationalUnits.Lookup))
        || User.IsInRole(SystemRoles.SuperAdmin);

    private AppModels.CreateLicenseRequestRequest MapCreateRequest(CreateLicenseRequestRequest request) =>
        new(
            request.RequestNumber,
            request.RequestSource,
            request.RequestDate,
            request.ExternalRequestNumber,
            request.EbysNumber,
            request.EbysDate,
            MapRequesterUnit(request.RequesterUnit),
            request.RequesterManagerName,
            request.Description,
            request.Status,
            request.EstimatedTotalCost,
            request.Currency,
            request.VatIncluded,
            request.CostNote,
            request.Items.Select(MapItem).ToList(),
            LicenseManagementActorResolver.ResolveActorUserId(User),
            LicenseManagementActorResolver.ResolveActorUserName(User),
            LicenseManagementActorResolver.ResolveIpAddress(this),
            LicenseManagementActorResolver.ResolveUserAgent(this));

    private AppModels.UpdateLicenseRequestRequest MapUpdateRequest(Guid id, UpdateLicenseRequestRequest request) =>
        new(
            id,
            request.RequestNumber,
            request.RequestSource,
            request.RequestDate,
            request.ExternalRequestNumber,
            request.EbysNumber,
            request.EbysDate,
            MapRequesterUnit(request.RequesterUnit),
            request.RequesterManagerName,
            request.Description,
            request.Status,
            request.EstimatedTotalCost,
            request.Currency,
            request.VatIncluded,
            request.CostNote,
            request.Items.Select(MapItem).ToList(),
            LicenseManagementActorResolver.ResolveActorUserId(User),
            LicenseManagementActorResolver.ResolveActorUserName(User),
            LicenseManagementActorResolver.ResolveIpAddress(this),
            LicenseManagementActorResolver.ResolveUserAgent(this));

    private static AppModels.LicenseRequestOuSnapshot MapRequesterUnit(LicenseRequestOuSnapshotRequest requesterUnit) =>
        new(
            requesterUnit.ObjectGuid,
            requesterUnit.DisplayName,
            requesterUnit.DistinguishedName);

    private static AppModels.LicenseRequestItemInput MapItem(LicenseRequestItemRequest item) =>
        new(
            item.ProductId,
            item.EstimatedUnitCost,
            item.Currency,
            item.VatIncluded,
            item.Justification,
            item.Status,
            item.Users.Select(user => new AppModels.LicenseRequestItemUserInput(
                user.AdObjectId,
                user.SamAccountName,
                user.UserPrincipalName,
                user.DisplayName,
                user.Department,
                user.Title,
                user.Mail,
                user.Phone,
                user.Status)).ToList());

    private static LicenseRequestDetailResponse MapDetail(AppModels.LicenseRequestDetail request) =>
        new(
            request.Id,
            request.RequestNumber,
            request.RequestSource,
            request.RequestDate,
            request.ExternalRequestNumber,
            request.EbysNumber,
            request.EbysDate,
            request.RequesterUnitDisplayName,
            request.RequesterUnitDistinguishedName,
            request.RequesterUnitObjectGuid,
            request.RequesterManagerName,
            request.Description,
            request.Status,
            request.EstimatedTotalCost,
            request.Currency,
            request.VatIncluded,
            request.CostNote,
            request.IsActive,
            request.Items.Select(item => new LicenseRequestItemResponse(
                item.Id,
                item.ProductId,
                item.ProductName,
                item.RequestedQuantity,
                item.ApprovedQuantity,
                item.FulfilledQuantity,
                item.EstimatedUnitCost,
                item.EstimatedTotalCost,
                item.Currency,
                item.VatIncluded,
                item.Justification,
                item.Status,
                item.Users.Select(user => new LicenseRequestItemUserResponse(
                    user.Id,
                    user.AdObjectId,
                    user.SamAccountName,
                    user.UserPrincipalName,
                    user.DisplayName,
                    user.Department,
                    user.Title,
                    user.Mail,
                    user.Phone,
                    user.Status)).ToList())).ToList(),
            request.CreatedAt,
            request.CreatedBy,
            request.UpdatedAt,
            request.UpdatedBy);
}
