using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using static ITAdmin.Persistence.Services.LicenseManagement.LicenseManagementServiceHelpers;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicensedProductService(AppDbContext context) : ILicensedProductService
{
    public async Task<PagedResult<LicensedProductListItem>> GetListAsync(
        LicensedProductListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);
        var itemsQuery = context.LicensedProducts
            .AsNoTracking()
            .Include(x => x.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.Name, pattern)
                || (x.Brand != null && EF.Functions.ILike(x.Brand, pattern))
                || EF.Functions.ILike(x.Category.Name, pattern));
        }

        if (query.IsActive is { } isActive)
        {
            itemsQuery = itemsQuery.Where(x => x.IsActive == isActive);
        }

        if (query.CategoryId is { } categoryId)
        {
            itemsQuery = itemsQuery.Where(x => x.CategoryId == categoryId);
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await itemsQuery
            .OrderBy(x => x.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LicensedProductListItem(
                x.Id,
                x.Name,
                x.Brand,
                x.CategoryId,
                x.Category.Name,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<LicensedProductListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<LicensedProductDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.LicensedProducts
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<LicensedProductOperationResult> CreateAsync(
        CreateLicensedProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateProductAsync(
            request.Name,
            request.Brand,
            request.CategoryId,
            null,
            cancellationToken);
        if (validationError is not null)
        {
            return new LicensedProductOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        var entity = new LicensedProduct
        {
            Name = request.Name.Trim(),
            Brand = LicenseManagementValidation.TrimOrNull(request.Brand),
            CategoryId = request.CategoryId,
            Description = LicenseManagementValidation.TrimOrNull(request.Description),
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedBy = request.ActorUserName
        };

        await context.LicensedProducts.AddAsync(entity, cancellationToken);
        await WriteAuditAsync(
            context,
            "Create",
            "LicensedProduct",
            entity.Id,
            $"Licensed product created: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await context.Entry(entity).Reference(x => x.Category).LoadAsync(cancellationToken);
        return new LicensedProductOperationResult(true, "Licensed product created.", Map(entity));
    }

    public async Task<LicensedProductOperationResult> UpdateAsync(
        UpdateLicensedProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicensedProducts
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicensedProductOperationResult(false, "Licensed product was not found.");
        }

        var validationError = await ValidateProductAsync(
            request.Name,
            request.Brand,
            request.CategoryId,
            entity.Id,
            cancellationToken);
        if (validationError is not null)
        {
            return new LicensedProductOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        entity.Name = request.Name.Trim();
        entity.Brand = LicenseManagementValidation.TrimOrNull(request.Brand);
        entity.CategoryId = request.CategoryId;
        entity.Description = LicenseManagementValidation.TrimOrNull(request.Description);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "Update",
            "LicensedProduct",
            entity.Id,
            $"Licensed product updated: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await context.Entry(entity).Reference(x => x.Category).LoadAsync(cancellationToken);
        return new LicensedProductOperationResult(true, "Licensed product updated.", Map(entity));
    }

    public async Task<LicensedProductOperationResult> UpdateStatusAsync(
        UpdateLicensedProductStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicensedProducts
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicensedProductOperationResult(false, "Licensed product was not found.");
        }

        if (entity.IsActive == request.IsActive)
        {
            return new LicensedProductOperationResult(true, "Licensed product status is unchanged.", Map(entity));
        }

        if (request.IsActive)
        {
            var duplicate = await context.LicensedProducts.AnyAsync(
                x => x.Id != entity.Id && x.IsActive && x.Name.ToLower() == entity.Name.ToLower(),
                cancellationToken);
            if (duplicate)
            {
                return new LicensedProductOperationResult(false, "An active product with the same name already exists.");
            }
        }

        var now = DateTime.UtcNow;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        var action = request.IsActive ? "Enable" : "Disable";
        await WriteAuditAsync(
            context,
            action,
            "LicensedProduct",
            entity.Id,
            $"Licensed product {(request.IsActive ? "activated" : "deactivated")}: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicensedProductOperationResult(true, "Licensed product status updated.", Map(entity));
    }

    private async Task<string?> ValidateProductAsync(
        string name,
        string? brand,
        Guid categoryId,
        Guid? currentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Product name is required.";
        }

        if (name.Trim().Length > 200)
        {
            return "Product name length is invalid.";
        }

        if (brand is { Length: > 200 })
        {
            return "Brand length is invalid.";
        }

        var normalizedName = name.Trim();
        var duplicate = await context.LicensedProducts.AnyAsync(
            x => x.IsActive
                 && (currentId == null || x.Id != currentId)
                 && x.Name.ToLower() == normalizedName.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            return "An active product with the same name already exists.";
        }

        var category = await context.LicenseProductCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return "Product category was not found.";
        }

        if (!category.IsActive)
        {
            return "Passive product categories cannot be used.";
        }

        return null;
    }

    private static LicensedProductDetail Map(LicensedProduct entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Brand,
            entity.CategoryId,
            entity.Category.Name,
            entity.Description,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);
}
