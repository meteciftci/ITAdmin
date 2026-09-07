using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using AppModels = ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/license-management")]
[Authorize]
public sealed class LicenseSeatAssignmentsController(ILicenseSeatAssignmentService seatService) : ControllerBase
{
    [HttpGet("packages/{packageId:guid}/seat-assignments")]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<LicensePackageSeatOverviewResponse>> GetByPackage(
        Guid packageId,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var overview = await seatService.GetByPackageAsync(packageId, includeInactive, cancellationToken);
        if (overview is null)
        {
            return NotFound(new { message = "License package was not found." });
        }

        return Ok(MapOverview(overview));
    }

    [HttpPost("packages/{packageId:guid}/seat-assignments")]
    [RequirePermission(LicenseManagementPermissions.FulfillRequests)]
    public async Task<ActionResult<LicenseSeatAssignmentOperationResponse>> Assign(
        Guid packageId,
        [FromBody] AssignLicenseSeatBody body,
        CancellationToken cancellationToken)
    {
        var result = await seatService.AssignAsync(
            new AppModels.AssignLicenseSeatRequest(
                packageId,
                MapPerson(body.Person),
                body.AssignedDate,
                body.SourceRequestItemId,
                body.Note,
                ResolveActor()),
            cancellationToken);

        return MapOperation(result);
    }

    [HttpPost("packages/{packageId:guid}/seat-assignments/copy-from/{sourcePackageId:guid}")]
    [RequirePermission(LicenseManagementPermissions.FulfillRequests)]
    public async Task<ActionResult<CopyLicenseSeatsResponse>> CopyFrom(
        Guid packageId,
        Guid sourcePackageId,
        [FromBody] CopyLicenseSeatsBody? body,
        CancellationToken cancellationToken)
    {
        var result = await seatService.CopySeatsAsync(
            new AppModels.CopyLicenseSeatsRequest(
                sourcePackageId,
                packageId,
                body?.AssignedDate,
                ResolveActor()),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new CopyLicenseSeatsResponse(false, result.Message, result.CopiedCount, result.SkippedCount));
        }

        return Ok(new CopyLicenseSeatsResponse(true, result.Message, result.CopiedCount, result.SkippedCount));
    }

    [HttpPost("seat-assignments/{id:guid}/release")]
    [RequirePermission(LicenseManagementPermissions.FulfillRequests)]
    public async Task<ActionResult<LicenseSeatAssignmentOperationResponse>> Release(
        Guid id,
        [FromBody] ReleaseLicenseSeatBody? body,
        CancellationToken cancellationToken)
    {
        var result = await seatService.ReleaseAsync(
            new AppModels.ReleaseLicenseSeatRequest(id, body?.ReleasedDate, body?.Note, ResolveActor()),
            cancellationToken);

        return MapOperation(result);
    }

    [HttpPost("seat-assignments/{id:guid}/transfer")]
    [RequirePermission(LicenseManagementPermissions.FulfillRequests)]
    public async Task<ActionResult<LicenseSeatAssignmentOperationResponse>> Transfer(
        Guid id,
        [FromBody] TransferLicenseSeatBody body,
        CancellationToken cancellationToken)
    {
        var result = await seatService.TransferAsync(
            new AppModels.TransferLicenseSeatRequest(
                id,
                MapPerson(body.NewPerson),
                body.TransferDate,
                body.Note,
                ResolveActor()),
            cancellationToken);

        return MapOperation(result);
    }

    private AppModels.LicenseSeatActorContext ResolveActor() =>
        new(
            LicenseManagementActorResolver.ResolveActorUserId(User),
            LicenseManagementActorResolver.ResolveActorUserName(User),
            LicenseManagementActorResolver.ResolveIpAddress(this),
            LicenseManagementActorResolver.ResolveUserAgent(this));

    private static AppModels.LicenseSeatPersonInput MapPerson(LicenseSeatPersonRequest person) =>
        new(
            person.AdObjectId,
            person.DisplayName,
            person.SamAccountName,
            person.UserPrincipalName,
            person.Mail,
            person.NationalId,
            person.Department,
            person.Title);

    private ActionResult<LicenseSeatAssignmentOperationResponse> MapOperation(AppModels.LicenseSeatAssignmentOperationResult result)
    {
        var payload = new LicenseSeatAssignmentOperationResponse(
            result.IsSuccess,
            result.Message,
            result.Assignment is null ? null : MapAssignment(result.Assignment));

        return result.IsSuccess ? Ok(payload) : BadRequest(payload);
    }

    private static LicensePackageSeatOverviewResponse MapOverview(AppModels.LicensePackageSeatOverview overview) =>
        new(
            overview.PackageId,
            overview.ProductName,
            overview.PurchaseTitle,
            overview.Quantity,
            overview.ActiveCount,
            overview.AvailableCount,
            overview.Assignments.Select(MapAssignment).ToList());

    private static LicenseSeatAssignmentResponse MapAssignment(AppModels.LicenseSeatAssignmentItem item) =>
        new(
            item.Id,
            item.PackageId,
            item.AdObjectId,
            item.DisplayName,
            item.SamAccountName,
            item.UserPrincipalName,
            item.Mail,
            item.NationalId,
            item.Department,
            item.Title,
            item.AssignedDate,
            item.ReleasedDate,
            item.Status,
            item.ReplacesAssignmentId,
            item.ReplacesDisplayName,
            item.SourceRequestItemId,
            item.Note,
            item.CreatedAt,
            item.CreatedBy,
            item.UpdatedAt,
            item.UpdatedBy);
}
