using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.Common;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using AppModels = ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Api.Controllers;

[ApiController]
[Route("api/license-management/companies")]
[Authorize]
public sealed class LicenseCompaniesController(ILicenseCompanyService companyService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<PagedResponse<LicenseCompanyListItemResponse>>> GetCompanies(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await companyService.GetListAsync(
            new AppModels.LicenseCompanyListQuery(search, isActive, pageNumber, pageSize),
            cancellationToken);

        return Ok(new PagedResponse<LicenseCompanyListItemResponse>(
            result.Items.Select(x => new LicenseCompanyListItemResponse(
                x.Id, x.Name, x.Email, x.Phone, x.ContactPersonName, x.ContactPersonPhone, x.IsActive)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<LicenseCompanyDetailResponse>> GetCompanyById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var company = await companyService.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return NotFound(new { message = "License company was not found." });
        }

        return Ok(MapDetail(company));
    }

    [HttpPost]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicenseCompanyDetailResponse>> CreateCompany(
        [FromBody] CreateLicenseCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await companyService.CreateAsync(
            new AppModels.CreateLicenseCompanyRequest(
                request.Name,
                request.Phone,
                request.Email,
                request.Website,
                request.ContactPersonName,
                request.ContactPersonPhone,
                request.ContactPersonEmail,
                request.Notes,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Company is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(nameof(GetCompanyById), new { id = result.Company.Id }, MapDetail(result.Company));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicenseCompanyDetailResponse>> UpdateCompany(
        Guid id,
        [FromBody] UpdateLicenseCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await companyService.UpdateAsync(
            new AppModels.UpdateLicenseCompanyRequest(
                id,
                request.Name,
                request.Phone,
                request.Email,
                request.Website,
                request.ContactPersonName,
                request.ContactPersonPhone,
                request.ContactPersonEmail,
                request.Notes,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Company is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Company));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicenseCompanyDetailResponse>> UpdateCompanyStatus(
        Guid id,
        [FromBody] UpdateLicenseCompanyStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await companyService.UpdateStatusAsync(
            new AppModels.UpdateLicenseCompanyStatusRequest(
                id,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Company is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Company));
    }

    private static LicenseCompanyDetailResponse MapDetail(AppModels.LicenseCompanyDetail company) =>
        new(
            company.Id,
            company.Name,
            company.Phone,
            company.Email,
            company.Website,
            company.ContactPersonName,
            company.ContactPersonPhone,
            company.ContactPersonEmail,
            company.Notes,
            company.IsActive,
            company.CreatedAt,
            company.CreatedBy,
            company.UpdatedAt,
            company.UpdatedBy);
}
