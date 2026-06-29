using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.Common;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Domain.Enums;
using AppModels = ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/license-management/packages")]
[Authorize]
public sealed class LicensePackagesController(ILicensePackageService packageService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<PagedResponse<LicensePackageListItemResponse>>> GetPackages(
        [FromQuery] string? search,
        [FromQuery] Guid? purchaseId,
        [FromQuery] Guid? productId,
        [FromQuery] LicensePackageStatus? status,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await packageService.GetListAsync(
            new AppModels.LicensePackageListQuery(
                search, purchaseId, productId, status, isActive, pageNumber, pageSize),
            cancellationToken);

        return Ok(new PagedResponse<LicensePackageListItemResponse>(
            result.Items.Select(x => new LicensePackageListItemResponse(
                x.Id,
                x.ProductName,
                x.PurchaseTitle,
                x.LicenseType,
                x.Quantity,
                x.UsedQuantity,
                x.AvailableQuantity,
                x.StartDate,
                x.EndDate,
                x.IsPerpetual,
                x.RenewalRequired,
                x.Status,
                x.IsActive)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<LicensePackageDetailResponse>> GetPackageById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var package = await packageService.GetByIdAsync(id, cancellationToken);
        if (package is null)
        {
            return NotFound(new { message = "License package was not found." });
        }

        return Ok(MapDetail(package));
    }

    [HttpPost]
    [RequirePermission(LicenseManagementPermissions.ManagePurchases)]
    public async Task<ActionResult<LicensePackageDetailResponse>> CreatePackage(
        [FromBody] CreateLicensePackageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await packageService.CreateAsync(
            new AppModels.CreateLicensePackageRequest(
                request.PurchaseId,
                request.ProductId,
                request.LicenseType,
                request.Quantity,
                request.StartDate,
                request.EndDate,
                request.IsPerpetual,
                request.RenewalRequired,
                request.RenewalDate,
                request.SerialNumber,
                request.LicenseKey,
                request.LicenseAccountEmail,
                request.LicensePortalUrl,
                request.LicenseNotes,
                request.IsActive,
                request.Status,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Package is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(nameof(GetPackageById), new { id = result.Package.Id }, MapDetail(result.Package));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.ManagePurchases)]
    public async Task<ActionResult<LicensePackageDetailResponse>> UpdatePackage(
        Guid id,
        [FromBody] UpdateLicensePackageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await packageService.UpdateAsync(
            new AppModels.UpdateLicensePackageRequest(
                id,
                request.PurchaseId,
                request.ProductId,
                request.LicenseType,
                request.Quantity,
                request.StartDate,
                request.EndDate,
                request.IsPerpetual,
                request.RenewalRequired,
                request.RenewalDate,
                request.SerialNumber,
                request.LicenseKey,
                request.LicenseAccountEmail,
                request.LicensePortalUrl,
                request.LicenseNotes,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Package is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Package));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(LicenseManagementPermissions.ManagePurchases)]
    public async Task<ActionResult<LicensePackageDetailResponse>> UpdatePackageStatus(
        Guid id,
        [FromBody] UpdateLicensePackageStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await packageService.UpdateStatusAsync(
            new AppModels.UpdateLicensePackageStatusRequest(
                id,
                request.Status,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Package is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Package));
    }

    private static LicensePackageDetailResponse MapDetail(AppModels.LicensePackageDetail package) =>
        new(
            package.Id,
            package.PurchaseId,
            package.PurchaseTitle,
            package.ProductId,
            package.ProductName,
            package.LicenseType,
            package.Quantity,
            package.UsedQuantity,
            package.AvailableQuantity,
            package.StartDate,
            package.EndDate,
            package.IsPerpetual,
            package.RenewalRequired,
            package.RenewalDate,
            package.SerialNumber,
            package.LicenseKey,
            package.LicenseAccountEmail,
            package.LicensePortalUrl,
            package.LicenseNotes,
            package.IsActive,
            package.Status,
            package.CreatedAt,
            package.CreatedBy,
            package.UpdatedAt,
            package.UpdatedBy);
}
