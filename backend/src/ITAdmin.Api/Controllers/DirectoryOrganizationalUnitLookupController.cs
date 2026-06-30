using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Security;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/license-management/directory-organizational-units")]
[Authorize]
public sealed class DirectoryOrganizationalUnitLookupController(
    IDirectoryOrganizationalUnitLookupService lookupService,
    IDirectoryOrganizationalUnitLookupReadinessService readinessService) : ControllerBase
{
    [HttpGet("search")]
    [RequirePermission(PermissionCodes.Directory.OrganizationalUnits.Lookup)]
    public async Task<ActionResult<DirectoryOrganizationalUnitLookupSearchResponse>> Search(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.SearchAsync(search, cancellationToken);
        return Ok(new DirectoryOrganizationalUnitLookupSearchResponse(
            result.IsSuccess,
            result.Message,
            result.Items.Select(item => new DirectoryOrganizationalUnitLookupItemResponse(
                item.ObjectGuid,
                item.DisplayName,
                item.Name,
                item.DistinguishedName)).ToList()));
    }

    [HttpGet("readiness")]
    [RequirePermission(LicenseManagementPermissions.ManageRequests)]
    public async Task<ActionResult<DirectoryUserLookupReadinessResponse>> GetReadiness(
        CancellationToken cancellationToken)
    {
        if (!HasOuLookupPermission())
        {
            return Forbid();
        }

        var result = await readinessService.CheckAsync(cancellationToken);
        return Ok(new DirectoryUserLookupReadinessResponse(
            result.IsReady,
            result.Reason,
            result.Message));
    }

    private bool HasOuLookupPermission() =>
        User.HasClaim(CustomClaimTypes.Permission, PermissionCodes.Directory.OrganizationalUnits.Lookup)
        || User.IsInRole(SystemRoles.SuperAdmin);
}
