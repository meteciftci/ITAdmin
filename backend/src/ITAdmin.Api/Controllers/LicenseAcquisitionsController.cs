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
[Route("api/license-management/acquisitions")]
[Authorize]
public sealed class LicenseAcquisitionsController(ILicenseAcquisitionService acquisitionService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<PagedResponse<LicenseAcquisitionListItemResponse>>> GetAcquisitions(
        [FromQuery] string? search,
        [FromQuery] LicenseAcquisitionType? acquisitionType,
        [FromQuery] LicenseAcquisitionStatus? status,
        [FromQuery] Guid? supplierCompanyId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await acquisitionService.GetListAsync(
            new AppModels.LicenseAcquisitionListQuery(
                search, acquisitionType, status, supplierCompanyId, pageNumber, pageSize),
            cancellationToken);

        return Ok(new PagedResponse<LicenseAcquisitionListItemResponse>(
            result.Items.Select(x => new LicenseAcquisitionListItemResponse(
                x.Id,
                x.Title,
                x.AcquisitionType,
                x.AcquisitionDate,
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
    public async Task<ActionResult<LicenseAcquisitionDetailResponse>> GetAcquisitionById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var acquisition = await acquisitionService.GetByIdAsync(id, cancellationToken);
        if (acquisition is null)
        {
            return NotFound(new { message = "License acquisition was not found." });
        }

        return Ok(MapDetail(acquisition));
    }

    [HttpPost]
    [RequirePermission(LicenseManagementPermissions.ManageAcquisitions)]
    public async Task<ActionResult<LicenseAcquisitionDetailResponse>> CreateAcquisition(
        [FromBody] CreateLicenseAcquisitionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await acquisitionService.CreateAsync(
            new AppModels.CreateLicenseAcquisitionRequest(
                request.AcquisitionType,
                request.Title,
                request.Description,
                request.AcquisitionDate,
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

        if (!result.IsSuccess || result.Acquisition is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(
            nameof(GetAcquisitionById),
            new { id = result.Acquisition.Id },
            MapDetail(result.Acquisition));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.ManageAcquisitions)]
    public async Task<ActionResult<LicenseAcquisitionDetailResponse>> UpdateAcquisition(
        Guid id,
        [FromBody] UpdateLicenseAcquisitionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await acquisitionService.UpdateAsync(
            new AppModels.UpdateLicenseAcquisitionRequest(
                id,
                request.AcquisitionType,
                request.Title,
                request.Description,
                request.AcquisitionDate,
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

        if (!result.IsSuccess || result.Acquisition is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Acquisition));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(LicenseManagementPermissions.ManageAcquisitions)]
    public async Task<ActionResult<LicenseAcquisitionDetailResponse>> UpdateAcquisitionStatus(
        Guid id,
        [FromBody] UpdateLicenseAcquisitionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await acquisitionService.UpdateStatusAsync(
            new AppModels.UpdateLicenseAcquisitionStatusRequest(
                id,
                request.Status,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Acquisition is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Acquisition));
    }

    private static LicenseAcquisitionDetailResponse MapDetail(AppModels.LicenseAcquisitionDetail acquisition) =>
        new(
            acquisition.Id,
            acquisition.AcquisitionType,
            acquisition.Title,
            acquisition.Description,
            acquisition.AcquisitionDate,
            acquisition.TenderNumber,
            acquisition.TenderDate,
            acquisition.DirectPurchaseNumber,
            acquisition.DmoOrderNumber,
            acquisition.EbysNumber,
            acquisition.EbysDate,
            acquisition.InvoiceNumber,
            acquisition.InvoiceDate,
            acquisition.ContractNumber,
            acquisition.ContractStartDate,
            acquisition.ContractEndDate,
            acquisition.SupplierCompanyId,
            acquisition.SupplierCompanyName,
            acquisition.SupportCompanyId,
            acquisition.SupportCompanyName,
            acquisition.ActualTotalCost,
            acquisition.Currency,
            acquisition.VatIncluded,
            acquisition.Notes,
            acquisition.Status,
            acquisition.CreatedAt,
            acquisition.CreatedBy,
            acquisition.UpdatedAt,
            acquisition.UpdatedBy);
}
