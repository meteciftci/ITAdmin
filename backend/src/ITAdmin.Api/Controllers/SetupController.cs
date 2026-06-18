using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Contracts.Setup;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Abstractions.Services;
using AppModels = ITAdmin.Application.Common.Models;

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
        [FromBody] ValidateLdapRequest request,
        CancellationToken cancellationToken)
    {
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        if (!isSetupRequired)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { messageKey = SetupApiMessageKeys.Validation.SetupAlreadyCompleted });
        }

        var result = await setupService.ValidateLdapAsync(
            new AppModels.ValidateSetupLdapRequest(
                request.SetupKey,
                MapLdapSettings(request)),
            cancellationToken);

        return Ok(new ValidateLdapResponse(result.IsValid, result.Message));
    }

    [HttpPost("search-admin-users")]
    public async Task<ActionResult<SearchSetupAdminUsersResponse>> SearchAdminUsers(
        [FromBody] SearchSetupAdminUsersRequest request,
        CancellationToken cancellationToken)
    {
        var isSetupRequired = await setupService.IsSetupRequiredAsync(cancellationToken);
        if (!isSetupRequired)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { messageKey = SetupApiMessageKeys.Validation.SetupAlreadyCompleted });
        }

        var result = await setupService.SearchAdminUsersAsync(
            new AppModels.SearchSetupAdminUsersRequest(
                request.SetupKey,
                MapLdapSettings(request.Ldap),
                request.Search),
            cancellationToken);

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
        [FromBody] CompleteSetupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await setupService.CompleteSetupAsync(
            new AppModels.CompleteSetupRequest(
                request.SetupKey,
                MapLdapSettings(request.Ldap),
                MapModules(request.Modules),
                request.AdminUsers
                    .Select(adminUser => new AppModels.CompleteSetupAdminUser(
                        adminUser.UserName,
                        adminUser.DistinguishedName,
                        adminUser.DirectoryObjectId))
                    .ToList()),
            cancellationToken);

        var response = new CompleteSetupResponse(result.IsCompleted, result.Message);
        if (result.IsCompleted)
        {
            return Ok(response);
        }

        return BadRequest(response);
    }

    private static AppModels.CompleteSetupLdapSettings MapLdapSettings(CompleteSetupLdapSettingsRequest ldap) =>
        new(
            ldap.Name,
            ldap.Host,
            ldap.BaseDn,
            ldap.UserSearchBase,
            ldap.UserSearchFilter,
            ldap.BindUserName,
            ldap.BindUserDomain,
            ldap.BindPassword);

    private static AppModels.CompleteSetupLdapSettings MapLdapSettings(ValidateLdapRequest request) =>
        new(
            Name: "Default LDAP",
            request.Host,
            request.BaseDn,
            request.UserSearchBase,
            request.UserSearchFilter,
            request.BindUserName,
            request.BindUserDomain,
            request.BindPassword);

    private static AppModels.CompleteSetupModulesSettings MapModules(CompleteSetupModulesRequest modules) =>
        new(modules.AdManagement is null
            ? null
            : new AppModels.CompleteSetupAdManagementModuleSettings(
                modules.AdManagement.IsEnabled,
                modules.AdManagement.UsersSearchBase,
                modules.AdManagement.GroupsSearchBase,
                modules.AdManagement.ComputersSearchBase,
                modules.AdManagement.DefaultUserOu,
                modules.AdManagement.DefaultGroupOu,
                modules.AdManagement.DefaultComputerOu,
                modules.AdManagement.DeletedObjectsEnabled));
}
