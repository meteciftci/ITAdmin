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
[Route("api/license-management/directory-user-lookup")]
[Authorize]
public sealed class DirectoryUserLookupController(
    IDirectoryUserLookupReadinessService readinessService) : ControllerBase
{
    [HttpGet("readiness")]
    [RequirePermission(LicenseManagementPermissions.ManageRequests)]
    public async Task<ActionResult<DirectoryUserLookupReadinessResponse>> GetReadiness(
        CancellationToken cancellationToken)
    {
        if (!HasDirectoryLookupPermission())
        {
            return Forbid();
        }

        var result = await readinessService.CheckAsync(cancellationToken);
        return Ok(new DirectoryUserLookupReadinessResponse(
            result.IsReady,
            result.Reason,
            result.Message));
    }

    private bool HasDirectoryLookupPermission() =>
        User.HasClaim(CustomClaimTypes.Permission, PermissionCodes.Directory.Users.Lookup)
        || User.IsInRole(SystemRoles.SuperAdmin);
}
