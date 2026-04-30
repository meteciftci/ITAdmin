using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Contracts.Setup;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/setup")]
public sealed class SetupController(ISetupService setupService, ILdapService ldapService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        return Ok(new SetupStatusResponse(isSetupRequired));
    }

    [HttpPost("validate-ldap")]
    public async Task<ActionResult<ValidateLdapResponse>> ValidateLdap(
        [FromBody] ValidateLdapRequest request,
        CancellationToken cancellationToken)
    {
        var validationRequest = new LdapValidationRequest
        {
            Host = request.Host,
            Port = request.Port,
            UseSsl = request.UseSsl,
            BaseDn = request.BaseDn,
            UserSearchBase = request.UserSearchBase,
            UserSearchFilter = request.UserSearchFilter,
            BindDn = request.BindDn,
            BindPassword = request.BindPassword,
            TestUserName = request.TestUserName,
            TestPassword = request.TestPassword
        };

        var result = await ldapService.ValidateAsync(validationRequest, cancellationToken);
        return Ok(new ValidateLdapResponse(result.IsValid, result.Message));
    }
}
