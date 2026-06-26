using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Contracts.Setup;
using ITAdmin.Api.Setup;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Abstractions.Services;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/setup")]
public sealed class SetupController(
    ISetupService setupService,
    ISetupPreflightService setupPreflightService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        return Ok(new SetupStatusResponse(isSetupRequired));
    }

    [HttpGet("preflight")]
    public async Task<ActionResult<SetupPreflightResponse>> GetPreflight(CancellationToken cancellationToken)
    {
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        if (!isSetupRequired)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { messageKey = SetupApiMessageKeys.Validation.SetupAlreadyCompleted });
        }

        var result = await setupPreflightService.CheckAsync(cancellationToken);
        var checks = result.Checks
            .Select(check => new SetupPreflightCheckResponse(
                check.Key,
                check.Status,
                check.MessageKey,
                check.Detail))
            .ToList();

        return Ok(new SetupPreflightResponse(checks, result.CanContinue));
    }

    [HttpPost("validate-ldap")]
    public async Task<ActionResult<ValidateLdapResponse>> ValidateLdap(
        [FromBody] ValidateLdapRequest? request,
        CancellationToken cancellationToken)
    {
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        if (!isSetupRequired)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { messageKey = SetupApiMessageKeys.Validation.SetupAlreadyCompleted });
        }

        if (!SetupControllerRequestMapper.TryMapValidateLdapRequest(request, out var mappedRequest, out var messageKey))
        {
            return BadRequest(new { messageKey });
        }

        var result = await setupService.ValidateLdapAsync(mappedRequest, cancellationToken);

        return Ok(new ValidateLdapResponse(result.IsValid, result.Message));
    }

    [HttpPost("search-admin-users")]
    public async Task<ActionResult<SearchSetupAdminUsersResponse>> SearchAdminUsers(
        [FromBody] SearchSetupAdminUsersRequest? request,
        CancellationToken cancellationToken)
    {
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        if (!isSetupRequired)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { messageKey = SetupApiMessageKeys.Validation.SetupAlreadyCompleted });
        }

        if (!SetupControllerRequestMapper.TryMapSearchAdminUsersRequest(request, out var mappedRequest, out var messageKey))
        {
            return BadRequest(new { messageKey });
        }

        var result = await setupService.SearchAdminUsersAsync(mappedRequest, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        var users = result.Users
            .Select(user => new SetupAdminUserSearchResultResponse(
                user.UserName,
                user.DisplayName,
                user.Email,
                user.DistinguishedName,
                user.DirectoryObjectId))
            .ToList();

        return Ok(new SearchSetupAdminUsersResponse(users));
    }

    [HttpPost("complete")]
    public async Task<ActionResult<CompleteSetupResponse>> CompleteSetup(
        [FromBody] CompleteSetupRequest? request,
        CancellationToken cancellationToken)
    {
        if (!SetupControllerRequestMapper.TryMapCompleteSetupRequest(request, out var mappedRequest, out var messageKey))
        {
            return BadRequest(new { messageKey });
        }

        var result = await setupService.CompleteSetupAsync(mappedRequest, cancellationToken);

        var response = new CompleteSetupResponse(result.IsCompleted, result.Message);
        if (result.IsCompleted)
        {
            return Ok(response);
        }

        return BadRequest(response);
    }
}
