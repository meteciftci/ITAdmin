using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Contracts.Setup;
using SasPortal.Application.Abstractions.Services;

namespace SasPortal.Api.Controllers;

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
