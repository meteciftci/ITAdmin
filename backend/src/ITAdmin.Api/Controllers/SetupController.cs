using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Contracts.Setup;
using ITAdmin.Application.Abstractions.Services;

namespace ITAdmin.Api.Controllers;

/// <summary>
/// First-run status only. ITAdmin is brought to a serving, logged-in-able state entirely by the
/// Windows installer (database provisioning, migrations, and the directory-backed initial
/// administrator via <c>ITAdmin.Api.exe --bootstrap-directory</c>). There is no in-application
/// setup wizard, so this controller exposes just the one read the installer's health check and the
/// SPA's root redirect rely on.
/// </summary>
[ApiController]
[Route("api/setup")]
public sealed class SetupController(ISetupService setupService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        return Ok(new SetupStatusResponse(isSetupRequired));
    }
}
