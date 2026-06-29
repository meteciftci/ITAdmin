using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using AppModels = ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/license-management/settings")]
[Authorize]
public sealed class LicenseManagementSettingsController(
    ILicenseManagementSettingsService settingsService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.ManageSettings)]
    public async Task<ActionResult<LicenseManagementSettingsResponse>> GetSettings(
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        return Ok(MapSettings(settings));
    }

    [HttpPut]
    [RequirePermission(LicenseManagementPermissions.ManageSettings)]
    public async Task<ActionResult<LicenseManagementSettingsResponse>> UpdateSettings(
        [FromBody] UpdateLicenseManagementSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateSettingsAsync(
            new AppModels.UpdateLicenseManagementSettingsRequest(
                request.DefaultCurrency,
                request.DefaultVatIncluded,
                request.DefaultRenewalReminderDays,
                request.DefaultRenewalRecipients,
                request.DefaultRenewalCcRecipients,
                request.Notes,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Settings is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapSettings(result.Settings));
    }

    private static LicenseManagementSettingsResponse MapSettings(AppModels.LicenseManagementSettingsModel settings) =>
        new(
            settings.DefaultCurrency,
            settings.DefaultVatIncluded,
            settings.DefaultRenewalReminderDays,
            settings.DefaultRenewalRecipients,
            settings.DefaultRenewalCcRecipients,
            settings.Notes,
            settings.UpdatedAt,
            settings.UpdatedBy);
}
