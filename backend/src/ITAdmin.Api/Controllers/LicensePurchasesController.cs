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
[Route("api/license-management/purchases")]
[Route("api/license-management/acquisitions")]
[Authorize]
public sealed class LicensePurchasesController(ILicensePurchaseService purchaseService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<PagedResponse<LicensePurchaseListItemResponse>>> GetPurchases(
        [FromQuery] string? search,
        [FromQuery] LicensePurchaseType? purchaseType,
        [FromQuery] LicensePurchaseStatus? status,
        [FromQuery] Guid? supplierCompanyId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await purchaseService.GetListAsync(
            new AppModels.LicensePurchaseListQuery(
                search, purchaseType, status, supplierCompanyId, pageNumber, pageSize),
            cancellationToken);

        return Ok(new PagedResponse<LicensePurchaseListItemResponse>(
            result.Items.Select(x => new LicensePurchaseListItemResponse(
                x.Id,
                x.Title,
                x.PurchaseType,
                x.PurchaseDate,
                x.SupplierCompanyName,
                x.SupportCompanyName,
                x.ContractNumber,
                x.Status)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<LicensePurchaseDetailResponse>> GetPurchaseById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var purchase = await purchaseService.GetByIdAsync(id, cancellationToken);
        if (purchase is null)
        {
            return NotFound(new { message = "License purchase was not found." });
        }

        return Ok(MapDetail(purchase));
    }

    [HttpPost]
    [RequirePermission(LicenseManagementPermissions.ManagePurchases)]
    public async Task<ActionResult<LicensePurchaseDetailResponse>> CreatePurchase(
        [FromBody] CreateLicensePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await purchaseService.CreateAsync(
            new AppModels.CreateLicensePurchaseRequest(
                request.PurchaseType,
                request.Title,
                request.Description,
                request.PurchaseDate,
                request.TenderNumber,
                request.TenderDate,
                request.DirectPurchaseNumber,
                request.DmoOrderNumber,
                request.EbysNumber,
                request.EbysDate,
                request.InvoiceNumber,
                request.InvoiceDate,
                request.ContractNumber,
                request.ContractStartDate,
                request.ContractEndDate,
                request.SupplierCompanyId,
                request.SupportCompanyId,
                request.ActualTotalCost,
                request.Currency,
                request.VatIncluded,
                request.Notes,
                request.Status,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Purchase is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(
            nameof(GetPurchaseById),
            new { id = result.Purchase.Id },
            MapDetail(result.Purchase));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.ManagePurchases)]
    public async Task<ActionResult<LicensePurchaseDetailResponse>> UpdatePurchase(
        Guid id,
        [FromBody] UpdateLicensePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await purchaseService.UpdateAsync(
            new AppModels.UpdateLicensePurchaseRequest(
                id,
                request.PurchaseType,
                request.Title,
                request.Description,
                request.PurchaseDate,
                request.TenderNumber,
                request.TenderDate,
                request.DirectPurchaseNumber,
                request.DmoOrderNumber,
                request.EbysNumber,
                request.EbysDate,
                request.InvoiceNumber,
                request.InvoiceDate,
                request.ContractNumber,
                request.ContractStartDate,
                request.ContractEndDate,
                request.SupplierCompanyId,
                request.SupportCompanyId,
                request.ActualTotalCost,
                request.Currency,
                request.VatIncluded,
                request.Notes,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Purchase is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Purchase));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(LicenseManagementPermissions.ManagePurchases)]
    public async Task<ActionResult<LicensePurchaseDetailResponse>> UpdatePurchaseStatus(
        Guid id,
        [FromBody] UpdateLicensePurchaseStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await purchaseService.UpdateStatusAsync(
            new AppModels.UpdateLicensePurchaseStatusRequest(
                id,
                request.Status,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Purchase is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Purchase));
    }

    private static LicensePurchaseDetailResponse MapDetail(AppModels.LicensePurchaseDetail purchase) =>
        new(
            purchase.Id,
            purchase.PurchaseType,
            purchase.Title,
            purchase.Description,
            purchase.PurchaseDate,
            purchase.TenderNumber,
            purchase.TenderDate,
            purchase.DirectPurchaseNumber,
            purchase.DmoOrderNumber,
            purchase.EbysNumber,
            purchase.EbysDate,
            purchase.InvoiceNumber,
            purchase.InvoiceDate,
            purchase.ContractNumber,
            purchase.ContractStartDate,
            purchase.ContractEndDate,
            purchase.SupplierCompanyId,
            purchase.SupplierCompanyName,
            purchase.SupportCompanyId,
            purchase.SupportCompanyName,
            purchase.ActualTotalCost,
            purchase.Currency,
            purchase.VatIncluded,
            purchase.Notes,
            purchase.Status,
            purchase.CreatedAt,
            purchase.CreatedBy,
            purchase.UpdatedAt,
            purchase.UpdatedBy);
}
