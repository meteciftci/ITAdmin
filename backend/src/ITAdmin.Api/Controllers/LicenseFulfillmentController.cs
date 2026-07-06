using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.Common;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using AppModels = ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/license-management/fulfillment")]
[Authorize]
public sealed class LicenseFulfillmentController(
    ILicenseRequestFulfillmentService fulfillmentService) : ControllerBase
{
    [HttpGet("candidates")]
    [RequireAnyPermission(LicenseManagementPermissions.FulfillRequests, LicenseManagementPermissions.View)]
    public async Task<ActionResult<PagedResponse<LicenseFulfillmentCandidateResponse>>> GetCandidates(
        [FromQuery] string? search,
        [FromQuery] Guid? productId,
        [FromQuery] string? requesterUnitObjectGuid,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await fulfillmentService.GetCandidatesAsync(
            new AppModels.LicenseFulfillmentCandidateQuery(search, productId, requesterUnitObjectGuid, pageNumber, pageSize),
            cancellationToken);

        return Ok(new PagedResponse<LicenseFulfillmentCandidateResponse>(
            result.Items.Select(x => new LicenseFulfillmentCandidateResponse(
                x.RequestId,
                x.RequestItemId,
                x.RequestSource,
                x.RequestDate,
                x.RequesterUnitDisplayName,
                x.ProductId,
                x.ProductName,
                x.ProductBrand,
                x.RequestedQuantity,
                x.ApprovedQuantity,
                x.FulfilledQuantity,
                x.RemainingQuantity,
                x.ItemStatus)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpPost("triage")]
    [RequirePermission(LicenseManagementPermissions.FulfillRequests)]
    public async Task<ActionResult> Triage(
        [FromBody] TriageLicenseRequestItemsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await fulfillmentService.TriageAsync(
            new AppModels.TriageLicenseRequestItemsRequest(
                request.Items
                    .Select(x => new AppModels.TriageLicenseRequestItemInput(x.RequestItemId, x.Status, x.ApprovedQuantity))
                    .ToList(),
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        return result.IsSuccess
            ? Ok(new { message = result.Message })
            : BadRequest(new { message = result.Message });
    }

    [HttpPost("convert")]
    [RequirePermission(LicenseManagementPermissions.FulfillRequests)]
    public async Task<ActionResult<LicenseFulfillmentResponse>> Convert(
        [FromBody] ConvertLicenseRequestItemsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await fulfillmentService.ConvertToPurchaseAsync(
            new AppModels.ConvertLicenseRequestItemsRequest(
                request.ExistingPurchaseId,
                request.NewPurchase is null
                    ? null
                    : new AppModels.ConvertFulfillmentNewPurchaseInput(
                        request.NewPurchase.PurchaseType,
                        request.NewPurchase.Title,
                        request.NewPurchase.Description,
                        request.NewPurchase.PurchaseDate,
                        request.NewPurchase.SupplierCompanyId,
                        request.NewPurchase.SupportCompanyId,
                        request.NewPurchase.ActualTotalCost,
                        request.NewPurchase.Currency,
                        request.NewPurchase.VatIncluded,
                        request.NewPurchase.Notes),
                request.Lines
                    .Select(x => new AppModels.ConvertFulfillmentLineInput(x.RequestItemId, x.FulfillQuantity))
                    .ToList(),
                request.PackageDefaults
                    .Select(x => new AppModels.ConvertFulfillmentPackageDefaultsInput(
                        x.ProductId, x.LicenseType, x.StartDate, x.EndDate, x.IsPerpetual))
                    .ToList(),
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { message = result.Message })
                : BadRequest(new { message = result.Message });
        }

        return Ok(new LicenseFulfillmentResponse(
            true,
            result.Message,
            result.PurchaseId,
            result.PackageIds ?? []));
    }
}
