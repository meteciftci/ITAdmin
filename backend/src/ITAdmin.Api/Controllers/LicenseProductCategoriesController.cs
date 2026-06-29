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
[Route("api/license-management/product-categories")]
[Authorize]
public sealed class LicenseProductCategoriesController(
    ILicenseProductCategoryService categoryService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<PagedResponse<LicenseProductCategoryListItemResponse>>> GetCategories(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await categoryService.GetListAsync(
            new AppModels.LicenseProductCategoryListQuery(search, isActive, pageNumber, pageSize),
            cancellationToken);

        return Ok(new PagedResponse<LicenseProductCategoryListItemResponse>(
            result.Items.Select(x => new LicenseProductCategoryListItemResponse(
                x.Id, x.Name, x.Description, x.IsActive)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("all")]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<IReadOnlyList<LicenseProductCategoryListItemResponse>>> GetAllActiveCategories(
        CancellationToken cancellationToken)
    {
        var items = await categoryService.GetAllActiveAsync(cancellationToken);
        return Ok(items.Select(x => new LicenseProductCategoryListItemResponse(
            x.Id, x.Name, x.Description, x.IsActive)).ToList());
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<LicenseProductCategoryDetailResponse>> GetCategoryById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await categoryService.GetByIdAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound(new { message = "License product category was not found." });
        }

        return Ok(MapDetail(category));
    }

    [HttpPost]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicenseProductCategoryDetailResponse>> CreateCategory(
        [FromBody] CreateLicenseProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.CreateAsync(
            new AppModels.CreateLicenseProductCategoryRequest(
                request.Name,
                request.Description,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Category is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(nameof(GetCategoryById), new { id = result.Category.Id }, MapDetail(result.Category));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicenseProductCategoryDetailResponse>> UpdateCategory(
        Guid id,
        [FromBody] UpdateLicenseProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.UpdateAsync(
            new AppModels.UpdateLicenseProductCategoryRequest(
                id,
                request.Name,
                request.Description,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Category is null)
        {
            return result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { message = result.Message })
                : BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Category));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicenseProductCategoryDetailResponse>> UpdateCategoryStatus(
        Guid id,
        [FromBody] UpdateLicenseProductCategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await categoryService.UpdateStatusAsync(
            new AppModels.UpdateLicenseProductCategoryStatusRequest(
                id,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Category is null)
        {
            return result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { message = result.Message })
                : BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Category));
    }

    private static LicenseProductCategoryDetailResponse MapDetail(AppModels.LicenseProductCategoryDetail category) =>
        new(
            category.Id,
            category.Name,
            category.Description,
            category.IsActive,
            category.CreatedAt,
            category.CreatedBy,
            category.UpdatedAt,
            category.UpdatedBy);
}
