using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Authorization;
using SasPortal.Api.Contracts.AdManagement;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using AppModels = SasPortal.Application.Common.Models;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/ad-management")]
[Authorize]
public sealed class AdManagementController(
    IAdManagementSettingsService settingsService,
    IAdAttributeMappingService attributeMappingService,
    IAdManagementValidationService validationService,
    IAdUserDirectoryService adUserDirectoryService,
    IAdUserAccountOperationService adUserAccountOperationService,
    IAdUserGroupMembershipService adUserGroupMembershipService,
    IAdUserOuMoveService adUserOuMoveService,
    IAdUserManagerUpdateService adUserManagerUpdateService,
    IAdUserAccountExpirationUpdateService adUserAccountExpirationUpdateService,
    IAdGroupDirectoryService adGroupDirectoryService,
    IAdComputerDirectoryService adComputerDirectoryService,
    IAdComputerAccountOperationService adComputerAccountOperationService,
    IAdComputerUpdateService adComputerUpdateService,
    IAdComputerOuMoveService adComputerOuMoveService,
    IAdComputerDeleteService adComputerDeleteService,
    IAdComputerGroupMembershipService adComputerGroupMembershipService,
    IAdDeletedObjectDirectoryService adDeletedObjectDirectoryService,
    IAdDeletedObjectRestoreService adDeletedObjectRestoreService,
    IAdDeletedObjectRestoreReadinessService adDeletedObjectRestoreReadinessService) : ControllerBase
{
    [HttpGet("settings")]
    [RequirePermission(AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<AdManagementSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        return Ok(MapSettings(settings));
    }

    [HttpPut("settings")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdManagementSettingsResponse>> UpdateSettings(
        [FromBody] AdManagementSettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateSettingsAsync(
            new AppModels.UpdateAdManagementSettingsRequest(
                request.IsEnabled,
                request.DomainFqdn,
                request.DefaultUserCreationUpnSuffix,
                request.NetbiosDomainName,
                request.DefaultNamingContext,
                request.BaseDn,
                request.UsersRootOu,
                request.DisabledUsersOu,
                request.GroupsSearchBase,
                request.ComputersSearchBase,
                request.PreferredDomainControllers,
                request.UseSsl,
                request.LdapPort,
                request.ServiceAccountUserName,
                request.ServiceAccountPassword,
                request.ClearServiceAccountPassword,
                request.PowerShellHealthEnabled,
                request.PowerShellTimeoutSeconds,
                MapNotificationSettingsRequest(request.NotificationSettings),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Settings is null)
        {
            if (result.Validation is not null)
            {
                return BadRequest(new
                {
                    message = result.Message,
                    validation = MapValidation(result.Validation)
                });
            }

            return BadRequest(new { message = result.Message });
        }

        return Ok(MapSettings(result.Settings));
    }

    [HttpPost("settings/validate")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdManagementValidationResponse>> ValidateSettings(
        CancellationToken cancellationToken)
    {
        var validationRequest = new AppModels.AdManagementValidationRequest(
            ResolveActorUserId(User),
            ResolveActorUserName(User),
            ResolveIpAddress(),
            ResolveUserAgent());

        var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);
        var primaryDc = ResolvePrimaryDomainController(connection);

        var result = connection is null
            ? BuildMissingConnectionValidationResult()
            : await validationService.ValidateConnectionAsync(
                connection,
                validationRequest,
                cancellationToken);

        await settingsService.RecordValidationResultAsync(
            result,
            validationRequest,
            primaryDc,
            cancellationToken);

        var restoreReadiness = await adDeletedObjectRestoreReadinessService.CheckAsync(cancellationToken);

        return Ok(MapValidation(result, restoreReadiness));
    }

    [HttpGet("deleted-objects/restore-readiness")]
    [RequirePermission(AdManagementPermissions.DeletedObjectsRestore)]
    public async Task<ActionResult<AdDeletedObjectRestoreReadinessResponse>> GetDeletedObjectRestoreReadiness(
        CancellationToken cancellationToken)
    {
        var result = await adDeletedObjectRestoreReadinessService.CheckAsync(cancellationToken);
        return Ok(MapRestoreReadiness(result));
    }

    private static string? ResolvePrimaryDomainController(
        AppModels.AdManagementConnectionParameters? connection)
    {
        if (connection is null)
        {
            return null;
        }

        if (connection.PreferredDomainControllers.Count > 0)
        {
            var first = connection.PreferredDomainControllers[0];
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return string.IsNullOrWhiteSpace(connection.DomainFqdn) ? null : connection.DomainFqdn;
    }

    private static AppModels.AdManagementValidationResult BuildMissingConnectionValidationResult()
    {
        var messageKey = AdManagementApiMessageKeys.SettingsValidation.MissingRequiredSettings;
        var message = AdManagementApiMessages.Legacy(messageKey);
        return new AppModels.AdManagementValidationResult(
            false,
            message,
            DateTimeOffset.UtcNow,
            new List<AppModels.AdManagementValidationDetail>
            {
                new("serviceAccountBind", AdManagementValidationStatuses.Failed, message, messageKey),
            },
            messageKey);
    }

    [HttpGet("users")]
    [RequirePermission(AdManagementPermissions.UsersView)]
    public async Task<ActionResult<AdUserSearchResponse>> SearchUsers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var statusFilter = ParseUserStatusFilter(status);
        var result = await adUserDirectoryService.SearchUsersAsync(
            new AppModels.AdUserSearchQuery(search, statusFilter, pageNumber, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdUserSearchResponse(
            result.Page.Items.Select(MapUserListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("groups")]
    [RequirePermission(AdManagementPermissions.GroupsView)]
    public async Task<ActionResult<AdGroupListResponse>> ListGroups(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await adGroupDirectoryService.SearchGroupsAsync(
            new AppModels.AdGroupListQuery(search, page, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdGroupListResponse(
            result.Page.Items.Select(MapGroupListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("groups/{id}")]
    [RequirePermission(AdManagementPermissions.GroupsView)]
    public async Task<ActionResult<AdGroupDetailResponse>> GetGroupById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.InvalidGroupId), messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var result = await adGroupDirectoryService.GetGroupByIdAsync(objectGuid, cancellationToken);
        if (!result.IsSuccess || result.Group is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapGroupDetail(result.Group));
    }

    [HttpGet("computers")]
    [RequirePermission(AdManagementPermissions.ComputersView)]
    public async Task<ActionResult<AdComputerListResponse>> ListComputers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? operatingSystem,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var statusFilter = ParseUserStatusFilter(status);
        var result = await adComputerDirectoryService.SearchComputersAsync(
            new AppModels.AdComputerListQuery(
                search,
                statusFilter,
                string.IsNullOrWhiteSpace(operatingSystem) ? null : operatingSystem.Trim(),
                pageNumber,
                pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdComputerListResponse(
            result.Page.Items.Select(MapComputerListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("computers/{id}")]
    [RequirePermission(AdManagementPermissions.ComputersView)]
    public async Task<ActionResult<AdComputerDetailResponse>> GetComputerById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.InvalidComputerId), messageKey = AdManagementApiMessageKeys.Computers.InvalidComputerId });
        }

        var result = await adComputerDirectoryService.GetComputerByIdAsync(objectGuid, cancellationToken);
        if (!result.IsSuccess || result.Computer is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapComputerDetail(result.Computer));
    }

    [HttpGet("deleted-objects")]
    [RequirePermission(AdManagementPermissions.DeletedObjectsView)]
    public async Task<ActionResult<AdDeletedObjectListResponse>> ListDeletedObjects(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAll = false,
        CancellationToken cancellationToken = default)
    {
        var result = await adDeletedObjectDirectoryService.SearchDeletedObjectsAsync(
            new AppModels.AdDeletedObjectSearchQuery(
                search,
                ParseDeletedObjectTypeFilter(type),
                pageNumber,
                pageSize,
                includeAll),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdDeletedObjectListResponse(
            result.Page.Items.Select(MapDeletedObjectListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.HasNextPage));
    }

    [HttpGet("deleted-objects/{id}")]
    [RequirePermission(AdManagementPermissions.DeletedObjectsView)]
    public async Task<ActionResult<AdDeletedObjectDetailResponse>> GetDeletedObjectById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.DeletedObjects.NotFound), messageKey = AdManagementApiMessageKeys.DeletedObjects.NotFound });
        }

        var result = await adDeletedObjectDirectoryService.GetDeletedObjectByIdAsync(
            objectGuid,
            cancellationToken);
        if (!result.IsSuccess || result.Object is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapDeletedObjectDetail(result.Object));
    }

    [HttpPost("deleted-objects/{id}/restore")]
    [RequirePermission(AdManagementPermissions.DeletedObjectsRestore)]
    public async Task<ActionResult<AdDeletedObjectRestoreResponse>> RestoreDeletedObject(
        [FromRoute] string id,
        [FromBody] AdDeletedObjectRestoreRequestBody? body,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.DeletedObjects.NotFound), messageKey = AdManagementApiMessageKeys.DeletedObjects.NotFound });
        }

        if (!AdDeletedObjectRestoreTargetModeParser.TryParse(
                body?.RestoreTargetMode,
                out var restoreTargetMode))
        {
            return BadRequest(new { message = "Geçersiz geri yükleme hedef modu." });
        }

        if (restoreTargetMode == AppModels.AdDeletedObjectRestoreTargetMode.TargetPath
            && string.IsNullOrWhiteSpace(body?.TargetPathDistinguishedName))
        {
            return BadRequest(new { message = "Farklı OU'ya geri yüklemek için hedef OU seçilmelidir." });
        }

        var result = await adDeletedObjectRestoreService.RestoreDeletedObjectAsync(
            new AppModels.AdDeletedObjectRestoreRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent(),
                restoreTargetMode,
                body?.TargetPathDistinguishedName?.Trim()),
            cancellationToken);

        if (!result.IsSuccess || result.RestoredObject is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdDeletedObjectRestoreResponse(
            true,
            result.Message,
            result.RestoredObject.ObjectId,
            result.RestoredObject.ObjectType.ToString(),
            result.RestoredObject.Name,
            result.RestoredObject.SamAccountName,
            result.RestoredObject.DistinguishedName,
            result.RestoredObject.RestoredParent,
            result.MessageKey,
            result.MessageParams));
    }

    [HttpGet("computer-operating-systems")]
    [RequirePermission(AdManagementPermissions.ComputersView)]
    public async Task<ActionResult<AdComputerOperatingSystemOptionsResponse>> GetComputerOperatingSystems(
        CancellationToken cancellationToken = default)
    {
        var result = await adComputerDirectoryService.GetComputerOperatingSystemsAsync(cancellationToken);
        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdComputerOperatingSystemOptionsResponse(result.Page.Items));
    }

    [HttpPost("computers/{id}/enable")]
    [RequirePermission(AdManagementPermissions.ComputersEnable)]
    public async Task<ActionResult<AdComputerAccountOperationResponse>> EnableComputer(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteComputerAccountOperationAsync(
            id,
            adComputerAccountOperationService.EnableComputerAsync,
            cancellationToken);

    [HttpPost("computers/{id}/disable")]
    [RequirePermission(AdManagementPermissions.ComputersDisable)]
    public async Task<ActionResult<AdComputerAccountOperationResponse>> DisableComputer(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteComputerAccountOperationAsync(
            id,
            adComputerAccountOperationService.DisableComputerAsync,
            cancellationToken);

    [HttpPut("computers/{id}")]
    [RequirePermission(AdManagementPermissions.ComputersUpdate)]
    public async Task<ActionResult<AdComputerAccountOperationResponse>> UpdateComputer(
        [FromRoute] string id,
        [FromBody] UpdateAdComputerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdComputerAccountOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.InvalidComputerId),
                null,
                AdManagementApiMessageKeys.Computers.InvalidComputerId));
        }

        var result = await adComputerUpdateService.UpdateComputerAsync(
            new AppModels.UpdateAdComputerRequest(
                objectGuid,
                request.Description,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        return MapComputerOperationActionResult(result.IsSuccess, result.Message, result.Computer, result.FailureKind, result.MessageKey, result.MessageParams);
    }

    [HttpPost("computers/{id}/move-ou")]
    [RequirePermission(AdManagementPermissions.ComputersMoveOu)]
    public async Task<ActionResult<AdComputerAccountOperationResponse>> MoveComputerOu(
        [FromRoute] string id,
        [FromBody] MoveAdComputerOuRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdComputerAccountOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.InvalidComputerId),
                null,
                AdManagementApiMessageKeys.Computers.InvalidComputerId));
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            return BadRequest(new AdComputerAccountOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.TargetOuRequired),
                null,
                AdManagementApiMessageKeys.Computers.TargetOuRequired));
        }

        var result = await adComputerOuMoveService.MoveOuAsync(
            new AppModels.MoveAdComputerOuRequest(
                objectGuid,
                request.TargetOuDistinguishedName.Trim(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        return MapComputerOperationActionResult(result.IsSuccess, result.Message, result.Computer, result.FailureKind, result.MessageKey, result.MessageParams);
    }

    [HttpGet("computers/{id}/groups")]
    [RequirePermission(AdManagementPermissions.ComputersGroupsView)]
    public async Task<ActionResult<AdComputerDirectGroupMembershipsResponse>> GetComputerGroups(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.InvalidComputerId), messageKey = AdManagementApiMessageKeys.Computers.InvalidComputerId });
        }

        var result = await adComputerGroupMembershipService.GetComputerGroupsAsync(
            new AppModels.AdComputerGroupMembershipRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapComputerGroupMemberships(result));
    }

    [HttpGet("computers/{id}/group-candidates")]
    [RequirePermission(AdManagementPermissions.ComputersGroupsAdd)]
    public async Task<ActionResult<AdComputerGroupCandidateSearchResponse>> SearchComputerGroupCandidates(
        [FromRoute] string id,
        [FromQuery] string? query,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.InvalidComputerId), messageKey = AdManagementApiMessageKeys.Computers.InvalidComputerId });
        }

        var result = await adComputerGroupMembershipService.SearchGroupCandidatesAsync(
            new AppModels.AdComputerGroupSearchRequest(objectGuid, query),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdComputerGroupCandidateSearchResponse(
            result.Items?
                .Select(item => new AdComputerGroupCandidateItemResponse(
                    item.DistinguishedName,
                    item.DisplayName,
                    item.Name,
                    item.SamAccountName,
                    item.Description))
                .ToList() ?? []));
    }

    [HttpPost("computers/{id}/groups")]
    [RequirePermission(AdManagementPermissions.ComputersGroupsAdd)]
    public async Task<ActionResult<AdComputerGroupOperationResponse>> AddComputerToGroup(
        [FromRoute] string id,
        [FromBody] AdComputerGroupMutationRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteComputerGroupOperationAsync(
            id,
            request.GroupDistinguishedName,
            (computerId, groupDn, cancellation) =>
                adComputerGroupMembershipService.AddComputerToGroupAsync(
                    new AppModels.AddAdComputerToGroupRequest(
                        computerId,
                        groupDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpDelete("computers/{id}/groups")]
    [RequirePermission(AdManagementPermissions.ComputersGroupsRemove)]
    public async Task<ActionResult<AdComputerGroupOperationResponse>> RemoveComputerFromGroup(
        [FromRoute] string id,
        [FromBody] AdComputerGroupMutationRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteComputerGroupOperationAsync(
            id,
            request.GroupDistinguishedName,
            (computerId, groupDn, cancellation) =>
                adComputerGroupMembershipService.RemoveComputerFromGroupAsync(
                    new AppModels.RemoveAdComputerFromGroupRequest(
                        computerId,
                        groupDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpDelete("computers/{id}")]
    [RequirePermission(AdManagementPermissions.ComputersDelete)]
    public async Task<ActionResult<DeleteAdComputerResponse>> DeleteComputer(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new DeleteAdComputerResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.InvalidComputerId),
                null,
                null,
                null,
                AdManagementApiMessageKeys.Computers.InvalidComputerId));
        }

        var result = await adComputerDeleteService.DeleteComputerAsync(
            new AppModels.DeleteAdComputerRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new DeleteAdComputerResponse(
            true,
            result.Message,
            result.DeletedComputerId,
            result.DeletedComputerName,
            result.DeletedDistinguishedName,
            result.MessageKey,
            result.MessageParams));
    }

    [HttpGet("computer-organizational-units")]
    [RequirePermission(AdManagementPermissions.ComputersView)]
    public async Task<ActionResult<AdOrganizationalUnitSearchResponse>> SearchComputerOrganizationalUnits(
        [FromQuery] string? search,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await adComputerDirectoryService.SearchComputerOrganizationalUnitsAsync(
            new AppModels.AdOrganizationalUnitSearchQuery(search, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdOrganizationalUnitSearchResponse(
            result.Page.Items
                .Select(item => new AdOrganizationalUnitListItemResponse(
                    item.DistinguishedName,
                    item.Name,
                    item.DisplayName,
                    item.Ou,
                    item.Label))
                .ToList(),
            result.Page.HasMore));
    }

    [HttpPost("groups")]
    [RequirePermission(AdManagementPermissions.GroupsCreate)]
    public async Task<ActionResult<AdGroupDetailResponse>> CreateGroup(
        [FromBody] CreateAdGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await adGroupDirectoryService.CreateGroupAsync(
            new AppModels.CreateAdGroupRequest(
                request.DisplayName,
                request.Name,
                request.SamAccountName,
                request.Description,
                request.GroupScope,
                request.TargetOuDistinguishedName,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Group is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapGroupDetail(result.Group));
    }

    [HttpPut("groups/{id}")]
    [RequirePermission(AdManagementPermissions.GroupsUpdate)]
    public async Task<ActionResult<AdGroupDetailResponse>> UpdateGroup(
        [FromRoute] string id,
        [FromBody] UpdateAdGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.InvalidGroupId), messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var result = await adGroupDirectoryService.UpdateGroupAsync(
            new AppModels.UpdateAdGroupRequest(
                objectGuid,
                request.DisplayName,
                request.Name,
                request.SamAccountName,
                request.Description,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Group is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapGroupDetail(result.Group));
    }

    [HttpDelete("groups/{id}")]
    [RequirePermission(AdManagementPermissions.GroupsDelete)]
    public async Task<ActionResult<DeleteAdGroupResponse>> DeleteGroup(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.InvalidGroupId), messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var result = await adGroupDirectoryService.DeleteGroupAsync(
            new AppModels.DeleteAdGroupRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new DeleteAdGroupResponse(
            true,
            result.Message,
            result.DeletedGroupId,
            result.MessageKey,
            result.MessageParams));
    }

    [HttpPost("groups/{id}/move-ou")]
    [RequirePermission(AdManagementPermissions.GroupsMoveOu)]
    public async Task<ActionResult<MoveAdGroupOuResponse>> MoveGroupOu(
        [FromRoute] string id,
        [FromBody] MoveAdGroupOuRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new MoveAdGroupOuResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.InvalidGroupId),
                id,
                null,
                null,
                null,
                null,
                null,
                request.TargetOuDistinguishedName,
                AdManagementApiMessageKeys.Groups.InvalidGroupId));
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            return BadRequest(new MoveAdGroupOuResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.TargetOuRequired),
                id,
                null,
                null,
                null,
                null,
                null,
                request.TargetOuDistinguishedName,
                AdManagementApiMessageKeys.Groups.TargetOuRequired));
        }

        var result = await adGroupDirectoryService.MoveGroupOuAsync(
            new AppModels.MoveAdGroupOuRequest(
                objectGuid,
                request.TargetOuDistinguishedName.Trim(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = new MoveAdGroupOuResponse(
            result.IsSuccess,
            result.Message,
            result.GroupId ?? id,
            result.DisplayName,
            result.Name,
            result.SamAccountName,
            result.DistinguishedName,
            result.PreviousDistinguishedName,
            result.TargetOuDistinguishedName,
            result.MessageKey,
            result.MessageParams);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            _ => BadRequest(response),
        };
    }

    [HttpGet("groups/{id}/members")]
    [RequirePermission(AdManagementPermissions.GroupsView)]
    public async Task<ActionResult<AdGroupMembersListResponse>> GetGroupMembers(
        [FromRoute] string id,
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.InvalidGroupId), messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var result = await adGroupDirectoryService.GetGroupMembersAsync(
            new AppModels.AdGroupMembersListQuery(objectGuid, search, type, pageNumber, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdGroupMembersListResponse(
            result.Page.Items.Select(MapGroupMemberListItem).ToList(),
            result.Page.PageNumber,
            result.Page.PageSize,
            result.Page.MemberCount,
            result.Page.HasNextPage));
    }

    [HttpGet("groups/{id}/member-candidates")]
    [RequirePermission(AdManagementPermissions.GroupsManageMembers)]
    public async Task<ActionResult<AdGroupMemberCandidatesResponse>> SearchGroupMemberCandidates(
        [FromRoute] string id,
        [FromQuery] string? search,
        [FromQuery] string? types,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.InvalidGroupId), messageKey = AdManagementApiMessageKeys.Groups.InvalidGroupId });
        }

        var typeList = string.IsNullOrWhiteSpace(types)
            ? Array.Empty<string>()
            : types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = await adGroupDirectoryService.SearchGroupMemberCandidatesAsync(
            new AppModels.AdGroupMemberCandidatesQuery(objectGuid, search, typeList, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Items is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdGroupMemberCandidatesResponse(
            result.Items.Select(MapGroupMemberCandidateItem).ToList()));
    }

    [HttpPost("groups/{id}/members")]
    [RequirePermission(AdManagementPermissions.GroupsManageMembers)]
    public async Task<ActionResult<AdGroupMemberOperationResponse>> AddGroupMember(
        [FromRoute] string id,
        [FromBody] AddAdGroupMemberRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteGroupMemberOperationAsync(
            id,
            request.MemberDistinguishedName,
            request.MemberType,
            (groupId, memberDn, memberType, cancellation) =>
                adGroupDirectoryService.AddGroupMemberAsync(
                    new AppModels.AddAdGroupMemberRequest(
                        groupId,
                        memberDn,
                        memberType,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpDelete("groups/{id}/members")]
    [RequirePermission(AdManagementPermissions.GroupsManageMembers)]
    public async Task<ActionResult<AdGroupMemberOperationResponse>> RemoveGroupMember(
        [FromRoute] string id,
        [FromBody] RemoveAdGroupMemberRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteGroupMemberOperationAsync(
            id,
            request.MemberDistinguishedName,
            memberType: null,
            (groupId, memberDn, _, cancellation) =>
                adGroupDirectoryService.RemoveGroupMemberAsync(
                    new AppModels.RemoveAdGroupMemberRequest(
                        groupId,
                        memberDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpGet("group-organizational-units")]
    [RequireAnyPermission(
        AdManagementPermissions.GroupsCreate,
        AdManagementPermissions.GroupsMoveOu)]
    public async Task<ActionResult<AdOrganizationalUnitSearchResponse>> SearchGroupOrganizationalUnits(
        [FromQuery] string? search,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await adGroupDirectoryService.SearchGroupOrganizationalUnitsAsync(
            new AppModels.AdOrganizationalUnitSearchQuery(search, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdOrganizationalUnitSearchResponse(
            result.Page.Items
                .Select(item => new AdOrganizationalUnitListItemResponse(
                    item.DistinguishedName,
                    item.Name,
                    item.DisplayName,
                    item.Ou,
                    item.Label))
                .ToList(),
            result.Page.HasMore));
    }

    [HttpGet("upn-suffixes")]
    [RequireAnyPermission(
        AdManagementPermissions.UsersCreate,
        AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<AdUpnSuffixesResponse>> GetUpnSuffixes(
        CancellationToken cancellationToken = default)
    {
        var result = await adUserDirectoryService.GetUpnSuffixesAsync(cancellationToken);
        if (!result.IsSuccess || result.Items is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdUpnSuffixesResponse(
            result.Items
                .Select(item => new AdUpnSuffixItemResponse(item.Value, item.Source))
                .ToList(),
            result.Warning));
    }

    [HttpGet("organizational-units")]
    [RequirePermission(AdManagementPermissions.UsersCreate)]
    public async Task<ActionResult<AdOrganizationalUnitSearchResponse>> SearchOrganizationalUnits(
        [FromQuery] string? search,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await adUserDirectoryService.SearchOrganizationalUnitsAsync(
            new AppModels.AdOrganizationalUnitSearchQuery(search, pageSize),
            cancellationToken);

        if (!result.IsSuccess || result.Page is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdOrganizationalUnitSearchResponse(
            result.Page.Items
                .Select(item => new AdOrganizationalUnitListItemResponse(
                    item.DistinguishedName,
                    item.Name,
                    item.DisplayName,
                    item.Ou,
                    item.Label))
                .ToList(),
            result.Page.HasMore));
    }

    [HttpPost("users")]
    [RequirePermission(AdManagementPermissions.UsersCreate)]
    public async Task<ActionResult<CreateAdUserResponse>> CreateUser(
        [FromBody] CreateAdUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await adUserDirectoryService.CreateUserAsync(
            new AppModels.CreateAdUserRequest(
                request.GivenName,
                request.Surname,
                request.Department,
                request.SamAccountName,
                request.UpnSuffix,
                request.TargetOuDistinguishedName,
                request.InitialPassword,
                request.IsEnabled,
                request.MustChangePasswordAtNextLogon,
                request.MappedAttributes
                    .Select(item => new AppModels.CreateAdUserMappedAttributeRequest(
                        item.LogicalField,
                        item.Value))
                    .ToList(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.User is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        var user = result.User;
        return Ok(new CreateAdUserResponse(
            user.Id,
            user.DistinguishedName,
            user.Cn,
            user.SamAccountName,
            user.UserPrincipalName,
            user.DisplayName,
            user.IsEnabled,
            user.Message,
            user.NamingCollisionResolved,
            user.GeneratedSuffix,
            user.NotificationSummary is null
                ? null
                : new AdUserCreatedNotificationSummaryResponse(
                    user.NotificationSummary.QueuedCount,
                    user.NotificationSummary.SkippedCount,
                    user.NotificationSummary.Messages),
            user.MessageKey,
            user.MessageParams));
    }

    [HttpPost("users/{id}/enable")]
    [RequirePermission(AdManagementPermissions.UsersEnable)]
    public async Task<ActionResult<AdUserAccountOperationResponse>> EnableUser(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteAccountOperationAsync(id, adUserAccountOperationService.EnableAsync, cancellationToken);

    [HttpPost("users/{id}/disable")]
    [RequirePermission(AdManagementPermissions.UsersDisable)]
    public async Task<ActionResult<AdUserAccountOperationResponse>> DisableUser(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteAccountOperationAsync(id, adUserAccountOperationService.DisableAsync, cancellationToken);

    [HttpPost("users/{id}/unlock")]
    [RequirePermission(AdManagementPermissions.UsersUnlock)]
    public async Task<ActionResult<AdUserAccountOperationResponse>> UnlockUser(
        [FromRoute] string id,
        CancellationToken cancellationToken = default) =>
        await ExecuteAccountOperationAsync(id, adUserAccountOperationService.UnlockAsync, cancellationToken);

    [HttpPut("users/{id}/manager")]
    [RequirePermission(AdManagementPermissions.UsersUpdate)]
    public async Task<ActionResult<UpdateAdUserManagerResponse>> UpdateUserManager(
        [FromRoute] string id,
        [FromBody] UpdateAdUserManagerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new UpdateAdUserManagerResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId),
                id,
                null,
                null,
                AdManagementApiMessageKeys.Users.InvalidUserId));
        }

        var result = await adUserManagerUpdateService.UpdateManagerAsync(
            new AppModels.UpdateAdUserManagerRequest(
                objectGuid,
                request.ManagerUserId,
                request.ClearManager,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = new UpdateAdUserManagerResponse(
            result.IsSuccess,
            result.Message,
            result.UserId ?? id,
            result.SamAccountName,
            result.ManagerDistinguishedName,
            result.ManagerDisplayName,
            result.MessageKey,
            result.MessageParams);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            _ => BadRequest(response),
        };
    }

    [HttpPut("users/{id}/account-expiration")]
    [RequirePermission(AdManagementPermissions.UsersUpdate)]
    public async Task<ActionResult<UpdateAdUserAccountExpirationResponse>> UpdateUserAccountExpiration(
        [FromRoute] string id,
        [FromBody] UpdateAdUserAccountExpirationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new UpdateAdUserAccountExpirationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId),
                id,
                null,
                null,
                request.NeverExpires,
                AdManagementApiMessageKeys.Users.InvalidUserId));
        }

        var result = await adUserAccountExpirationUpdateService.UpdateAccountExpirationAsync(
            new AppModels.UpdateAdUserAccountExpirationRequest(
                objectGuid,
                request.NeverExpires,
                request.ExpiresAt,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = new UpdateAdUserAccountExpirationResponse(
            result.IsSuccess,
            result.Message,
            result.UserId ?? id,
            result.SamAccountName,
            result.AccountExpiresDate,
            result.NeverExpires,
            result.MessageKey,
            result.MessageParams);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            _ => BadRequest(response),
        };
    }

    [HttpPost("users/{id}/move-ou")]
    [RequirePermission(AdManagementPermissions.UsersMoveOu)]
    public async Task<ActionResult<MoveAdUserOuResponse>> MoveUserOu(
        [FromRoute] string id,
        [FromBody] MoveAdUserOuRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new MoveAdUserOuResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId),
                id,
                null,
                null,
                null,
                null,
                request.TargetOuDistinguishedName,
                AdManagementApiMessageKeys.Users.InvalidUserId));
        }

        if (string.IsNullOrWhiteSpace(request.TargetOuDistinguishedName))
        {
            return BadRequest(new MoveAdUserOuResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.TargetOuRequired),
                id,
                null,
                null,
                null,
                null,
                request.TargetOuDistinguishedName,
                AdManagementApiMessageKeys.Users.TargetOuRequired));
        }

        var result = await adUserOuMoveService.MoveOuAsync(
            new AppModels.MoveAdUserOuRequest(
                objectGuid,
                request.TargetOuDistinguishedName.Trim(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = new MoveAdUserOuResponse(
            result.IsSuccess,
            result.Message,
            result.UserId ?? id,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.PreviousDistinguishedName,
            result.TargetOuDistinguishedName,
            result.MessageKey,
            result.MessageParams);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            _ => BadRequest(response),
        };
    }

    [HttpGet("users/{id}/groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsView)]
    public async Task<ActionResult<AdUserDirectGroupMembershipsResponse>> GetUserGroups(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId), messageKey = AdManagementApiMessageKeys.Users.InvalidUserId });
        }

        var result = await adUserGroupMembershipService.GetUserGroupsAsync(
            new AppModels.AdUserGroupMembershipRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapUserGroupMemberships(result));
    }

    [HttpGet("users/{id}/effective-groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsView)]
    public async Task<ActionResult<AdUserEffectiveGroupsResponse>> GetUserEffectiveGroups(
        [FromRoute] string id,
        [FromQuery] int? maxDepth,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId), messageKey = AdManagementApiMessageKeys.Users.InvalidUserId });
        }

        if (maxDepth.HasValue
            && (maxDepth.Value < AdEffectiveGroupMembershipLimits.MinMaxDepth
                || maxDepth.Value > AdEffectiveGroupMembershipLimits.MaxMaxDepth))
        {
            return BadRequest(new
            {
                message =
                    $"maxDepth {AdEffectiveGroupMembershipLimits.MinMaxDepth} ile {AdEffectiveGroupMembershipLimits.MaxMaxDepth} arasında olmalıdır.",
            });
        }

        var result = await adUserGroupMembershipService.GetUserEffectiveGroupsAsync(
            new AppModels.AdUserEffectiveGroupsRequest(objectGuid, maxDepth),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapUserEffectiveGroups(result));
    }

    [HttpGet("groups/search")]
    [RequirePermission(AdManagementPermissions.UsersGroupsView)]
    public async Task<ActionResult<AdGroupSearchResponse>> SearchGroups(
        [FromQuery] string? query,
        CancellationToken cancellationToken = default)
    {
        var result = await adUserGroupMembershipService.SearchGroupsAsync(
            new AppModels.AdGroupSearchRequest(query),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(new AdGroupSearchResponse(
            result.Items?
                .Select(item => new AdGroupSearchItemResponse(
                    item.DistinguishedName,
                    item.DisplayName,
                    item.Name,
                    item.SamAccountName,
                    item.Description))
                .ToList() ?? []));
    }

    [HttpPost("users/{id}/groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsAdd)]
    public async Task<ActionResult<AdUserGroupOperationResponse>> AddUserToGroup(
        [FromRoute] string id,
        [FromBody] AdUserGroupMutationRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteGroupOperationAsync(
            id,
            request.GroupDistinguishedName,
            (userId, groupDn, cancellation) =>
                adUserGroupMembershipService.AddUserToGroupAsync(
                    new AppModels.AddAdUserToGroupRequest(
                        userId,
                        groupDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpDelete("users/{id}/groups")]
    [RequirePermission(AdManagementPermissions.UsersGroupsRemove)]
    public async Task<ActionResult<AdUserGroupOperationResponse>> RemoveUserFromGroup(
        [FromRoute] string id,
        [FromBody] AdUserGroupMutationRequest request,
        CancellationToken cancellationToken = default) =>
        await ExecuteGroupOperationAsync(
            id,
            request.GroupDistinguishedName,
            (userId, groupDn, cancellation) =>
                adUserGroupMembershipService.RemoveUserFromGroupAsync(
                    new AppModels.RemoveAdUserFromGroupRequest(
                        userId,
                        groupDn,
                        ResolveActorUserId(User),
                        ResolveActorUserName(User),
                        ResolveIpAddress(),
                        ResolveUserAgent()),
                    cancellation),
            cancellationToken);

    [HttpPut("users/{id}")]
    [RequirePermission(AdManagementPermissions.UsersUpdate)]
    public async Task<ActionResult<AdUserDetailResponse>> UpdateUser(
        [FromRoute] string id,
        [FromBody] UpdateAdUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId), messageKey = AdManagementApiMessageKeys.Users.InvalidUserId });
        }

        var result = await adUserDirectoryService.UpdateUserAsync(
            new AppModels.UpdateAdUserRequest(
                objectGuid,
                request.GivenName,
                request.Surname,
                request.DisplayName,
                request.SamAccountName,
                request.UserPrincipalName,
                request.Mail,
                request.Department,
                request.MappedAttributes
                    .Select(item => new AppModels.UpdateAdUserMappedAttributeRequest(
                        item.LogicalField,
                        item.Value))
                    .ToList(),
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.User is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapUserDetail(result.User));
    }

    [HttpGet("users/{id}")]
    [RequirePermission(AdManagementPermissions.UsersView)]
    public async Task<ActionResult<AdUserDetailResponse>> GetUserById(
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new { message = AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId), messageKey = AdManagementApiMessageKeys.Users.InvalidUserId });
        }

        var result = await adUserDirectoryService.GetUserByIdAsync(objectGuid, cancellationToken);
        if (!result.IsSuccess || result.User is null)
        {
            return MapDirectoryFailure(result.Message, result.FailureKind, result.MessageKey, result.MessageParams);
        }

        return Ok(MapUserDetail(result.User));
    }

    [HttpGet("attribute-mappings")]
    [RequirePermission(AdManagementPermissions.SettingsView)]
    public async Task<ActionResult<IReadOnlyList<AdAttributeMappingResponse>>> GetMappings(
        CancellationToken cancellationToken)
    {
        var items = await attributeMappingService.GetMappingsAsync(cancellationToken);
        return Ok(items.Select(MapMapping).ToList());
    }

    [HttpPost("attribute-mappings")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdAttributeMappingResponse>> CreateMapping(
        [FromBody] CreateAdAttributeMappingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.CreateAsync(
            new AppModels.CreateAdAttributeMappingRequest(
                request.LogicalField,
                request.DisplayName,
                request.AttributeName,
                request.IsEnabled,
                request.IsEditable,
                request.IsSensitive,
                request.IsSearchable,
                request.ValidationType,
                request.MaskingStrategy,
                request.SortOrder,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Mapping is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapMapping(result.Mapping));
    }

    [HttpPut("attribute-mappings/{id:guid}")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult<AdAttributeMappingResponse>> UpdateMapping(
        [FromRoute] Guid id,
        [FromBody] UpdateAdAttributeMappingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.UpdateAsync(
            new AppModels.UpdateAdAttributeMappingRequest(
                id,
                request.DisplayName,
                request.AttributeName,
                request.IsEnabled,
                request.IsEditable,
                request.IsSensitive,
                request.IsSearchable,
                request.ValidationType,
                request.MaskingStrategy,
                request.SortOrder,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess || result.Mapping is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapMapping(result.Mapping));
    }

    [HttpDelete("attribute-mappings/{id:guid}")]
    [RequirePermission(AdManagementPermissions.SettingsUpdate)]
    public async Task<ActionResult> DeleteMapping(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await attributeMappingService.DeleteAsync(
            new AppModels.DeleteAdAttributeMappingRequest(
                id,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return NoContent();
    }

    private static AdManagementSettingsResponse MapSettings(AppModels.AdManagementSettingsModel settings) =>
        new(
            settings.IsConfigured,
            settings.IsEnabled,
            settings.DomainFqdn,
            settings.DefaultUserCreationUpnSuffix,
            settings.NetbiosDomainName,
            settings.DefaultNamingContext,
            settings.BaseDn,
            settings.UsersRootOu,
            settings.DisabledUsersOu,
            settings.GroupsSearchBase,
            settings.ComputersSearchBase,
            settings.PreferredDomainControllers,
            settings.UseSsl,
            settings.LdapPort,
            settings.ServiceAccountUserName,
            settings.HasServiceAccountPassword,
            settings.PowerShellHealthEnabled,
            settings.PowerShellTimeoutSeconds,
            settings.LastValidatedAt,
            settings.LastValidationStatus,
            settings.LastValidationMessage,
            MapNotificationSettingsResponse(settings.NotificationSettings));

    private static AdManagementNotificationSettings MapNotificationSettingsRequest(
        AdManagementNotificationSettingsRequest? request)
    {
        if (request is null)
        {
            return AdManagementNotificationSettingsSerializer.CreateDefault();
        }

        return new AdManagementNotificationSettings
        {
            Rules = request.Rules
                .Select(rule => new AdManagementNotificationRule
                {
                    Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id,
                    EventKey = rule.EventKey.Trim(),
                    Channel = rule.Channel.Trim(),
                    IsEnabled = rule.IsEnabled,
                    RecipientSource = MapRecipientSourceRequest(rule.RecipientSource),
                })
                .ToList(),
        };
    }

    private static AdManagementNotificationRecipientSource? MapRecipientSourceRequest(
        AdManagementNotificationRecipientSourceRequest? source) =>
        source is null || string.IsNullOrWhiteSpace(source.Type)
            ? null
            : new AdManagementNotificationRecipientSource
            {
                Type = source.Type.Trim(),
                Value = string.IsNullOrWhiteSpace(source.Value) ? null : source.Value.Trim(),
            };

    private static AdManagementNotificationSettingsResponse MapNotificationSettingsResponse(
        AdManagementNotificationSettings settings) =>
        new()
        {
            Rules = settings.Rules
                .Select(rule => new AdManagementNotificationRuleResponse
                {
                    Id = rule.Id,
                    EventKey = rule.EventKey,
                    Channel = rule.Channel,
                    IsEnabled = rule.IsEnabled,
                    RecipientSource = MapRecipientSourceResponse(rule.RecipientSource),
                })
                .ToList(),
        };

    private static AdManagementNotificationRecipientSourceResponse? MapRecipientSourceResponse(
        AdManagementNotificationRecipientSource? source) =>
        source is null || string.IsNullOrWhiteSpace(source.Type)
            ? null
            : new AdManagementNotificationRecipientSourceResponse
            {
                Type = source.Type,
                Value = source.Value,
            };

    private static AdAttributeMappingResponse MapMapping(AppModels.AdAttributeMappingItem item) =>
        new(
            item.Id,
            item.LogicalField,
            item.DisplayName,
            item.AttributeName,
            item.IsEnabled,
            item.IsEditable,
            item.IsSensitive,
            item.IsSearchable,
            item.ValidationType,
            item.MaskingStrategy,
            item.SortOrder);

    private static AdManagementValidationResponse MapValidation(
        AppModels.AdManagementValidationResult result,
        AppModels.AdDeletedObjectRestoreReadinessResult? restoreReadiness = null) =>
        new(
            result.IsValid,
            result.Message,
            result.CheckedAt,
            result.Details
                .Select(d => new AdManagementValidationDetailResponse(d.Key, d.Status, d.Message, d.MessageKey, d.MessageParams))
                .ToList(),
            restoreReadiness is null ? null : MapRestoreReadiness(restoreReadiness),
            result.MessageKey,
            result.MessageParams);

    private static AdDeletedObjectRestoreReadinessResponse MapRestoreReadiness(
        AppModels.AdDeletedObjectRestoreReadinessResult result) =>
        new(
            result.IsReady,
            result.Status,
            result.SummaryMessage,
            result.BlockingReasons.Select(MapRestoreReadinessCheck).ToList(),
            result.Warnings.Select(MapRestoreReadinessCheck).ToList(),
            result.Checks.Select(MapRestoreReadinessCheck).ToList(),
            result.CheckedAtUtc,
            result.DomainController,
            result.SummaryKey,
            result.SummaryParams);

    private static AdDeletedObjectRestoreReadinessCheckResponse MapRestoreReadinessCheck(
        AppModels.AdDeletedObjectRestoreReadinessCheck check) =>
        new(
            check.Key,
            check.Status,
            check.Title,
            check.Message,
            check.Remediation,
            check.Command,
            check.IsBlocking,
            check.Details,
            check.TitleKey,
            check.TitleParams,
            check.MessageKey,
            check.MessageParams,
            check.RemediationKey,
            check.RemediationParams);

    private static AppModels.AdUserStatusFilter ParseUserStatusFilter(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "disabled" => AppModels.AdUserStatusFilter.Disabled,
            "all" => AppModels.AdUserStatusFilter.All,
            _ => AppModels.AdUserStatusFilter.Active,
        };

    private static AppModels.AdDeletedObjectTypeFilter ParseDeletedObjectTypeFilter(string? type) =>
        type?.Trim().ToLowerInvariant() switch
        {
            "user" => AppModels.AdDeletedObjectTypeFilter.User,
            "group" => AppModels.AdDeletedObjectTypeFilter.Group,
            "computer" => AppModels.AdDeletedObjectTypeFilter.Computer,
            _ => AppModels.AdDeletedObjectTypeFilter.All,
        };

    private async Task<ActionResult<AdComputerGroupOperationResponse>> ExecuteComputerGroupOperationAsync(
        string id,
        string groupDistinguishedName,
        Func<Guid, string, CancellationToken, Task<AppModels.AdComputerGroupOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdComputerGroupOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.InvalidComputerId),
                id,
                null,
                null,
                groupDistinguishedName,
                null,
                null,
                null,
                AdManagementApiMessageKeys.Computers.InvalidComputerId));
        }

        if (string.IsNullOrWhiteSpace(groupDistinguishedName))
        {
            return BadRequest(new AdComputerGroupOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.GroupDnRequired),
                id,
                null,
                null,
                groupDistinguishedName,
                null,
                null,
                null,
                AdManagementApiMessageKeys.Groups.GroupDnRequired));
        }

        var result = await operation(
            objectGuid,
            groupDistinguishedName.Trim(),
            cancellationToken);

        var response = new AdComputerGroupOperationResponse(
            result.IsSuccess,
            result.Message,
            result.ComputerId,
            result.ComputerName,
            result.ComputerSamAccountName,
            result.GroupDistinguishedName,
            result.GroupName,
            result.GroupDisplayName,
            result.GroupSamAccountName,
            result.MessageKey,
            result.MessageParams);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            _ => BadRequest(response),
        };
    }

    private static AdComputerDirectGroupMembershipsResponse MapComputerGroupMemberships(
        AppModels.AdComputerGroupMembershipResult result) =>
        new(
            result.ComputerId ?? string.Empty,
            result.Name,
            result.SamAccountName,
            result.DnsHostName,
            result.DistinguishedName,
            result.Groups?
                .Select(group => new AdComputerGroupMembershipItemResponse(
                    group.Id,
                    group.DistinguishedName,
                    group.DisplayName,
                    group.Name,
                    group.SamAccountName,
                    group.Description,
                    group.IsDirect))
                .ToList() ?? []);

    private async Task<ActionResult<AdUserGroupOperationResponse>> ExecuteGroupOperationAsync(
        string id,
        string groupDistinguishedName,
        Func<Guid, string, CancellationToken, Task<AppModels.AdUserGroupOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdUserGroupOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId),
                id,
                groupDistinguishedName,
                null,
                AdManagementApiMessageKeys.Users.InvalidUserId));
        }

        if (string.IsNullOrWhiteSpace(groupDistinguishedName))
        {
            return BadRequest(new AdUserGroupOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.GroupDnRequired),
                id,
                groupDistinguishedName,
                null,
                AdManagementApiMessageKeys.Groups.GroupDnRequired));
        }

        var result = await operation(
            objectGuid,
            groupDistinguishedName.Trim(),
            cancellationToken);

        var response = new AdUserGroupOperationResponse(
            result.IsSuccess,
            result.Message,
            result.UserId,
            result.GroupDistinguishedName,
            result.GroupName,
            result.MessageKey,
            result.MessageParams);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            _ => BadRequest(response),
        };
    }

    private static AdUserDirectGroupMembershipsResponse MapUserGroupMemberships(
        AppModels.AdUserGroupMembershipResult result) =>
        new(
            result.UserId ?? string.Empty,
            result.DisplayName,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.Groups?
                .Select(group => new AdUserGroupMembershipItemResponse(
                    group.DistinguishedName,
                    group.DisplayName,
                    group.Name,
                    group.SamAccountName,
                    group.Description,
                    group.IsDirect))
                .ToList() ?? []);

    private static AdUserEffectiveGroupsResponse MapUserEffectiveGroups(
        AppModels.AdUserEffectiveGroupsResult result) =>
        new(
            result.UserId ?? string.Empty,
            result.DisplayName,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.DirectGroups?
                .Select(group => new AdEffectiveGroupSummaryItemResponse(
                    group.Name,
                    group.DistinguishedName,
                    group.SamAccountName,
                    group.Description,
                    group.DisplayName))
                .ToList() ?? [],
            result.EffectiveGroups?
                .Select(group => new AdEffectiveGroupNestedItemResponse(
                    group.Name,
                    group.DistinguishedName,
                    group.SamAccountName,
                    group.Description,
                    group.DisplayName,
                    group.Depth,
                    group.IsDirect,
                    group.Path
                        .Select(node => new AdMembershipPathNodeResponse(
                            node.Type,
                            node.Name,
                            node.DisplayName,
                            node.SamAccountName,
                            node.DistinguishedName))
                        .ToList()))
                .ToList() ?? [],
            result.MaxDepth,
            result.Truncated,
            result.TruncatedReason);

    private async Task<ActionResult<AdComputerAccountOperationResponse>> ExecuteComputerAccountOperationAsync(
        string id,
        Func<AppModels.AdComputerAccountOperationRequest, CancellationToken, Task<AppModels.AdComputerAccountOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdComputerAccountOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Computers.InvalidComputerId),
                null,
                AdManagementApiMessageKeys.Computers.InvalidComputerId));
        }

        var result = await operation(
            new AppModels.AdComputerAccountOperationRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = MapComputerAccountOperationResponse(result);
        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            AppModels.AdDirectoryFailureKind.Disabled
                or AppModels.AdDirectoryFailureKind.NotConfigured
                or AppModels.AdDirectoryFailureKind.MissingPassword
                or AppModels.AdDirectoryFailureKind.InvalidRequest => BadRequest(response),
            _ => BadRequest(response),
        };
    }

    private static AdComputerAccountOperationResponse MapComputerAccountOperationResponse(
        AppModels.AdComputerAccountOperationResult result) =>
        new(
            result.IsSuccess,
            result.Message,
            result.Computer is null ? null : MapComputerDetail(result.Computer),
            result.MessageKey,
            result.MessageParams);

    private ActionResult<AdComputerAccountOperationResponse> MapComputerOperationActionResult(
        bool isSuccess,
        string message,
        AppModels.AdComputerDetail? computer,
        AppModels.AdDirectoryFailureKind? failureKind,
        string? messageKey = null,
        IReadOnlyDictionary<string, object>? messageParams = null)
    {
        var response = new AdComputerAccountOperationResponse(
            isSuccess,
            message,
            computer is null ? null : MapComputerDetail(computer),
            messageKey,
            messageParams);

        if (isSuccess)
        {
            return Ok(response);
        }

        return failureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            AppModels.AdDirectoryFailureKind.Disabled
                or AppModels.AdDirectoryFailureKind.NotConfigured
                or AppModels.AdDirectoryFailureKind.MissingPassword
                or AppModels.AdDirectoryFailureKind.InvalidRequest => BadRequest(response),
            _ => BadRequest(response),
        };
    }

    private async Task<ActionResult<AdUserAccountOperationResponse>> ExecuteAccountOperationAsync(
        string id,
        Func<AppModels.AdUserAccountOperationRequest, CancellationToken, Task<AppModels.AdUserAccountOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdUserAccountOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Users.InvalidUserId),
                id,
                null,
                null,
                null,
                null,
                null,
                AdManagementApiMessageKeys.Users.InvalidUserId));
        }

        var result = await operation(
            new AppModels.AdUserAccountOperationRequest(
                objectGuid,
                ResolveActorUserId(User),
                ResolveActorUserName(User),
                ResolveIpAddress(),
                ResolveUserAgent()),
            cancellationToken);

        var response = MapAccountOperationResponse(result);
        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            AppModels.AdDirectoryFailureKind.Disabled
                or AppModels.AdDirectoryFailureKind.NotConfigured
                or AppModels.AdDirectoryFailureKind.MissingPassword => BadRequest(response),
            _ => BadRequest(response),
        };
    }

    private static AdUserAccountOperationResponse MapAccountOperationResponse(
        AppModels.AdUserAccountOperationResult result) =>
        new(
            result.IsSuccess,
            result.Message,
            result.UserId ?? string.Empty,
            result.SamAccountName,
            result.UserPrincipalName,
            result.DistinguishedName,
            result.IsEnabled,
            result.IsLockedOut,
            result.MessageKey,
            result.MessageParams);

    private ActionResult MapDirectoryFailure(
        string message,
        AppModels.AdDirectoryFailureKind? failureKind,
        string? messageKey = null,
        IReadOnlyDictionary<string, object>? messageParams = null) =>
        failureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(new { message, messageKey, messageParams }),
            AppModels.AdDirectoryFailureKind.InvalidRequest => BadRequest(new { message, messageKey, messageParams }),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message, messageKey, messageParams }),
            _ => BadRequest(new { message, messageKey, messageParams }),
        };

    private static AdUserListItemResponse MapUserListItem(AppModels.AdUserListItem item) =>
        new(
            item.Id,
            item.DistinguishedName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DisplayName,
            item.Mail,
            item.Department,
            item.IsEnabled,
            item.IsLockedOut,
            item.WhenCreated,
            item.WhenChanged,
            item.LastLogonAt);

    private static AdComputerListItemResponse MapComputerListItem(AppModels.AdComputerListItem item) =>
        new(
            item.Id,
            item.Name,
            item.SamAccountName,
            item.DnsHostName,
            item.OperatingSystem,
            item.DistinguishedName,
            item.IsEnabled,
            item.WhenChanged);

    private static AdComputerDetailResponse MapComputerDetail(AppModels.AdComputerDetail item) =>
        new(
            item.Id,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.DnsHostName,
            item.DistinguishedName,
            item.ParentOuDistinguishedName,
            item.Description,
            item.OperatingSystem,
            item.OperatingSystemVersion,
            item.OperatingSystemServicePack,
            item.ManagedByDistinguishedName,
            item.ManagedByDisplayName,
            item.LastLogonAt,
            item.WhenCreated,
            item.WhenChanged,
            item.UserAccountControl,
            item.IsEnabled,
            item.PrimaryGroupId,
            item.MemberOfCount,
            item.MemberOf.Select(MapComputerMemberOfItem).ToList(),
            item.MemberOfTruncated);

    private static AdComputerMemberOfItemResponse MapComputerMemberOfItem(
        AppModels.AdComputerMemberOfItem item) =>
        new(item.DistinguishedName, item.Name, item.SamAccountName);

    private static AdDeletedObjectListItemResponse MapDeletedObjectListItem(
        AppModels.AdDeletedObjectListItem item) =>
        new(
            item.Id,
            item.ObjectType.ToString(),
            item.Name,
            item.DisplayName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DistinguishedName,
            item.LastKnownParent,
            item.WhenChanged,
            item.DeletedAt);

    private static AdDeletedObjectDetailResponse MapDeletedObjectDetail(
        AppModels.AdDeletedObjectDetail item) =>
        new(
            item.Id,
            item.ObjectType.ToString(),
            item.Name,
            item.DisplayName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.Description,
            item.DistinguishedName,
            item.LastKnownParent,
            item.LastKnownRdn,
            item.ObjectClass,
            item.ObjectSid,
            item.WhenCreated,
            item.WhenChanged,
            item.DeletedAt,
            item.Mail,
            item.Department,
            item.DnsHostName,
            item.OperatingSystem,
            item.MemberOfCount,
            item.MemberOf,
            item.MemberOfTruncated,
            item.AdditionalAttributes);

    private static AdGroupListItemResponse MapGroupListItem(AppModels.AdGroupListItem item) =>
        new(
            item.Id,
            item.DistinguishedName,
            item.DisplayName,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.Description,
            item.GroupScope,
            item.SecurityEnabled,
            item.GroupType);

    private static AdGroupDetailResponse MapGroupDetail(AppModels.AdGroupDetail item) =>
        new(
            item.Id,
            item.DistinguishedName,
            item.DisplayName,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.Description,
            item.GroupScope,
            item.SecurityEnabled,
            item.GroupType,
            item.WhenCreated,
            item.WhenChanged,
            item.ManagedByDistinguishedName,
            item.ManagedByDisplayName,
            item.MemberCount,
            item.MemberOfCount,
            item.Members.Select(MapGroupMemberItem).ToList(),
            item.MemberOf.Select(MapGroupMemberItem).ToList(),
            item.MembersTruncated,
            item.MemberOfTruncated);

    private static AdGroupMemberItemResponse MapGroupMemberItem(AppModels.AdGroupMemberItem item) =>
        new(
            item.Type,
            item.DisplayName,
            item.Name,
            item.SamAccountName,
            item.DistinguishedName,
            item.Description);

    private static AdGroupMemberListItemResponse MapGroupMemberListItem(AppModels.AdGroupMemberListItem item) =>
        new(
            item.Id,
            item.Type,
            item.DisplayName,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DNSHostName,
            item.Description,
            item.DistinguishedName,
            item.IsDirectMember);

    private static AdGroupMemberCandidateItemResponse MapGroupMemberCandidateItem(
        AppModels.AdGroupMemberCandidateItem item) =>
        new(
            item.Id,
            item.Type,
            item.DisplayName,
            item.Name,
            item.Cn,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DNSHostName,
            item.Description,
            item.DistinguishedName,
            item.IsAlreadyDirectMember,
            item.IsEnabled);

    private async Task<ActionResult<AdGroupMemberOperationResponse>> ExecuteGroupMemberOperationAsync(
        string id,
        string memberDistinguishedName,
        string? memberType,
        Func<Guid, string, string?, CancellationToken, Task<AppModels.AdGroupMemberOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var objectGuid))
        {
            return BadRequest(new AdGroupMemberOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.InvalidGroupId),
                id,
                null,
                null,
                memberDistinguishedName,
                null,
                AdManagementApiMessageKeys.Groups.InvalidGroupId));
        }

        if (string.IsNullOrWhiteSpace(memberDistinguishedName))
        {
            return BadRequest(new AdGroupMemberOperationResponse(
                false,
                AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.Groups.MemberOperationFailed),
                id,
                null,
                null,
                memberDistinguishedName,
                null,
                AdManagementApiMessageKeys.Groups.MemberOperationFailed));
        }

        var result = await operation(
            objectGuid,
            memberDistinguishedName.Trim(),
            memberType,
            cancellationToken);

        var response = new AdGroupMemberOperationResponse(
            result.IsSuccess,
            result.Message,
            result.GroupId,
            result.GroupDistinguishedName,
            result.GroupName,
            result.MemberDistinguishedName,
            result.MemberName,
            result.MessageKey,
            result.MessageParams);

        if (result.IsSuccess)
        {
            return Ok(response);
        }

        return result.FailureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(response),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response),
            _ => BadRequest(response),
        };
    }

    private static AdUserDetailResponse MapUserDetail(AppModels.AdUserDetail item) =>
        new(
            item.Id,
            item.DistinguishedName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.DisplayName,
            item.Mail,
            item.GivenName,
            item.Surname,
            item.Department,
            item.IsEnabled,
            item.IsLockedOut,
            item.PasswordLastSetAt,
            item.LastLogonAt,
            item.WhenCreated,
            item.WhenChanged,
            item.UserAccountControl,
            item.AccountExpiresAt,
            item.AccountExpiresDate,
            item.LockoutTimeAt,
            item.BadPwdCount,
            item.BadPasswordTimeAt,
            item.LastLogonTimestampAt,
            item.Groups
                .Select(MapGroupMembership)
                .ToList(),
            item.MappedAttributes
                .Select(MapMappedAttribute)
                .ToList(),
            item.ManagerDistinguishedName,
            item.ManagerId,
            item.ManagerSamAccountName,
            item.ManagerUserPrincipalName,
            item.ManagerDisplayName);

    private static AdUserGroupMembershipResponse MapGroupMembership(AppModels.AdUserGroupMembership item) =>
        new(item.Name, item.DistinguishedName);

    private static MappedAdUserAttributeResponse MapMappedAttribute(AppModels.MappedAdUserAttribute item) =>
        new(
            item.LogicalField,
            item.DisplayName,
            item.AdAttribute,
            item.Value,
            item.IsSensitive,
            item.MaskingStrategy,
            item.IsEditable,
            item.IsSearchable,
            item.SortOrder);

    private static string? ResolveActorUserName(ClaimsPrincipal principal)
    {
        if (!string.IsNullOrWhiteSpace(principal.Identity?.Name))
        {
            return principal.Identity!.Name;
        }

        var nameClaim = principal.FindFirst(ClaimTypes.Name) ?? principal.FindFirst("name");
        return string.IsNullOrWhiteSpace(nameClaim?.Value) ? null : nameClaim.Value.Trim();
    }

    private static Guid? ResolveActorUserId(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private string? ResolveIpAddress()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? null : ip;
    }

    private string? ResolveUserAgent()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }
}
