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
[Route("api/license-management/products")]
[Authorize]
public sealed class LicensedProductsController(ILicensedProductService productService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<PagedResponse<LicensedProductListItemResponse>>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? categoryId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await productService.GetListAsync(
            new AppModels.LicensedProductListQuery(search, isActive, categoryId, pageNumber, pageSize),
            cancellationToken);

        return Ok(new PagedResponse<LicensedProductListItemResponse>(
            result.Items.Select(x => new LicensedProductListItemResponse(
                x.Id, x.Name, x.Brand, x.CategoryId, x.CategoryName, x.IsActive)).ToList(),
            result.PageNumber,
            result.PageSize,
            result.TotalCount,
            result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.View)]
    public async Task<ActionResult<LicensedProductDetailResponse>> GetProductById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await productService.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound(new { message = "Licensed product was not found." });
        }

        return Ok(MapDetail(product));
    }

    [HttpPost]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicensedProductDetailResponse>> CreateProduct(
        [FromBody] CreateLicensedProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.CreateAsync(
            new AppModels.CreateLicensedProductRequest(
                request.Name,
                request.Brand,
                request.CategoryId,
                request.Description,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Product is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return CreatedAtAction(nameof(GetProductById), new { id = result.Product.Id }, MapDetail(result.Product));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicensedProductDetailResponse>> UpdateProduct(
        Guid id,
        [FromBody] UpdateLicensedProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.UpdateAsync(
            new AppModels.UpdateLicensedProductRequest(
                id,
                request.Name,
                request.Brand,
                request.CategoryId,
                request.Description,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Product is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Product));
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(LicenseManagementPermissions.ManageCatalog)]
    public async Task<ActionResult<LicensedProductDetailResponse>> UpdateProductStatus(
        Guid id,
        [FromBody] UpdateLicensedProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await productService.UpdateStatusAsync(
            new AppModels.UpdateLicensedProductStatusRequest(
                id,
                request.IsActive,
                LicenseManagementActorResolver.ResolveActorUserId(User),
                LicenseManagementActorResolver.ResolveActorUserName(User),
                LicenseManagementActorResolver.ResolveIpAddress(this),
                LicenseManagementActorResolver.ResolveUserAgent(this)),
            cancellationToken);

        if (!result.IsSuccess || result.Product is null)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(MapDetail(result.Product));
    }

    private static LicensedProductDetailResponse MapDetail(AppModels.LicensedProductDetail product) =>
        new(
            product.Id,
            product.Name,
            product.Brand,
            product.CategoryId,
            product.CategoryName,
            product.Description,
            product.IsActive,
            product.CreatedAt,
            product.CreatedBy,
            product.UpdatedAt,
            product.UpdatedBy);
}
