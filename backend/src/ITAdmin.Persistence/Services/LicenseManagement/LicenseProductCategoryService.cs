using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using static ITAdmin.Persistence.Services.LicenseManagement.LicenseManagementServiceHelpers;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicenseProductCategoryService(AppDbContext context) : ILicenseProductCategoryService
{
    public async Task<PagedResult<LicenseProductCategoryListItem>> GetListAsync(
        LicenseProductCategoryListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);
        var itemsQuery = context.LicenseProductCategories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.Name, pattern)
                || (x.Description != null && EF.Functions.ILike(x.Description, pattern)));
        }

        if (query.IsActive is { } isActive)
        {
            itemsQuery = itemsQuery.Where(x => x.IsActive == isActive);
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await itemsQuery
            .OrderBy(x => x.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LicenseProductCategoryListItem(
                x.Id,
                x.Name,
                x.Description,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<LicenseProductCategoryListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<IReadOnlyList<LicenseProductCategoryListItem>> GetAllActiveAsync(
        CancellationToken cancellationToken = default) =>
        await context.LicenseProductCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new LicenseProductCategoryListItem(
                x.Id,
                x.Name,
                x.Description,
                x.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<LicenseProductCategoryDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseProductCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<LicenseProductCategoryOperationResult> CreateAsync(
        CreateLicenseProductCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateCategoryAsync(request.Name, null, cancellationToken);
        if (validationError is not null)
        {
            return new LicenseProductCategoryOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        var entity = new LicenseProductCategory
        {
            Name = request.Name.Trim(),
            Description = LicenseManagementValidation.TrimOrNull(request.Description),
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedBy = request.ActorUserName
        };

        await context.LicenseProductCategories.AddAsync(entity, cancellationToken);
        await WriteAuditAsync(
            context,
            "Create",
            "LicenseProductCategory",
            entity.Id,
            $"License product category created: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseProductCategoryOperationResult(true, "License product category created.", Map(entity));
    }

    public async Task<LicenseProductCategoryOperationResult> UpdateAsync(
        UpdateLicenseProductCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseProductCategories
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicenseProductCategoryOperationResult(false, "License product category was not found.");
        }

        var validationError = await ValidateCategoryAsync(request.Name, entity.Id, cancellationToken);
        if (validationError is not null)
        {
            return new LicenseProductCategoryOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        entity.Name = request.Name.Trim();
        entity.Description = LicenseManagementValidation.TrimOrNull(request.Description);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "Update",
            "LicenseProductCategory",
            entity.Id,
            $"License product category updated: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseProductCategoryOperationResult(true, "License product category updated.", Map(entity));
    }

    public async Task<LicenseProductCategoryOperationResult> UpdateStatusAsync(
        UpdateLicenseProductCategoryStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseProductCategories
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicenseProductCategoryOperationResult(false, "License product category was not found.");
        }

        if (entity.IsActive == request.IsActive)
        {
            return new LicenseProductCategoryOperationResult(
                true,
                "License product category status is unchanged.",
                Map(entity));
        }

        if (request.IsActive)
        {
            var duplicate = await context.LicenseProductCategories.AnyAsync(
                x => x.Id != entity.Id && x.IsActive && x.Name.ToLower() == entity.Name.ToLower(),
                cancellationToken);
            if (duplicate)
            {
                return new LicenseProductCategoryOperationResult(
                    false,
                    "An active category with the same name already exists.");
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
            "LicenseProductCategory",
            entity.Id,
            $"License product category {(request.IsActive ? "activated" : "deactivated")}: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseProductCategoryOperationResult(true, "License product category status updated.", Map(entity));
    }

    private async Task<string?> ValidateCategoryAsync(
        string name,
        Guid? currentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Category name is required.";
        }

        if (name.Trim().Length > 200)
        {
            return "Category name length is invalid.";
        }

        var normalizedName = name.Trim();
        var duplicate = await context.LicenseProductCategories.AnyAsync(
            x => x.IsActive
                 && (currentId == null || x.Id != currentId)
                 && x.Name.ToLower() == normalizedName.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            return "An active category with the same name already exists.";
        }

        return null;
    }

    private static LicenseProductCategoryDetail Map(LicenseProductCategory entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);
}
