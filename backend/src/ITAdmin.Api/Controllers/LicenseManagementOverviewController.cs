using ITAdmin.Application.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Application.Abstractions.Services;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/license-management/overview")]
[Authorize]
public sealed class LicenseManagementOverviewController(
    ILicenseManagementOverviewService overviewService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<LicenseManagementOverviewResponse>> GetOverview(
        CancellationToken cancellationToken)
    {
        var summary = await overviewService.GetSummaryAsync(cancellationToken);
        return Ok(new LicenseManagementOverviewResponse(
            summary.CompanyCount,
            summary.ActiveProductCount,
            summary.PurchaseCount,
            summary.PackageCount,
            summary.TotalLicenseQuantity));
    }
}
