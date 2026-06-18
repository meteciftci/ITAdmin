using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Contracts.Setup;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using AppModels = SasPortal.Application.Common.Models;

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
        // LDAP validation is a first-run setup capability only. Once setup is
        // completed the endpoint must not perform any outbound LDAP connection.
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        if (!isSetupRequired)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { messageKey = SetupApiMessageKeys.Validation.SetupAlreadyCompleted });
        }

        var validationRequest = new AppModels.LdapValidationRequest
        {
            Host = request.Host,
            BaseDn = request.BaseDn,
            UserSearchBase = request.UserSearchBase,
            UserSearchFilter = request.UserSearchFilter,
            BindUserName = request.BindUserName,
            BindUserDomain = request.BindUserDomain,
            BindPassword = request.BindPassword,
            TestUserName = request.TestUserName,
            TestPassword = request.TestPassword
        };

        var result = await ldapService.ValidateAsync(validationRequest, cancellationToken);
        return Ok(new ValidateLdapResponse(result.IsValid, result.Message));
    }

    [HttpPost("complete")]
    public async Task<ActionResult<CompleteSetupResponse>> CompleteSetup(
        [FromBody] CompleteSetupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await setupService.CompleteSetupAsync(
            new AppModels.CompleteSetupRequest(
                request.SetupKey,
                new AppModels.CompleteSetupLdapSettings(
                    request.Ldap.Name,
                    request.Ldap.Host,
                    request.Ldap.BaseDn,
                    request.Ldap.UserSearchBase,
                    request.Ldap.UserSearchFilter,
                    request.Ldap.BindUserName,
                    request.Ldap.BindUserDomain,
                    request.Ldap.BindPassword,
                    request.Ldap.NationalIdAttribute),
                new AppModels.CompleteSetupAdminUser(
                    request.Admin.UserName,
                    request.Admin.Password)),
            cancellationToken);

        var response = new CompleteSetupResponse(result.IsCompleted, result.Message);
        if (result.IsCompleted)
        {
            return Ok(response);
        }

        return BadRequest(response);
    }
}
