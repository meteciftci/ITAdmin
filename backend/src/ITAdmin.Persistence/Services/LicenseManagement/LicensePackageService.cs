using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using static ITAdmin.Persistence.Services.LicenseManagement.LicenseManagementServiceHelpers;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicensePackageService(AppDbContext context) : ILicensePackageService
{
    private const int UsedQuantity = 0;

    public async Task<PagedResult<LicensePackageListItem>> GetListAsync(
        LicensePackageListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);
        var itemsQuery = context.LicensePackages
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Acquisition)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.Product.Name, pattern)
                || EF.Functions.ILike(x.Acquisition.Title, pattern)
                || (x.SerialNumber != null && EF.Functions.ILike(x.SerialNumber, pattern)));
        }

        if (query.AcquisitionId is { } acquisitionId)
        {
            itemsQuery = itemsQuery.Where(x => x.AcquisitionId == acquisitionId);
        }

        if (query.ProductId is { } productId)
        {
            itemsQuery = itemsQuery.Where(x => x.ProductId == productId);
        }

        if (query.Status is { } status)
        {
            itemsQuery = itemsQuery.Where(x => x.Status == status);
        }

        if (query.IsActive is { } isActive)
        {
            itemsQuery = itemsQuery.Where(x => x.IsActive == isActive);
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await itemsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LicensePackageListItem(
                x.Id,
                x.Product.Name,
                x.Acquisition.Title,
                x.LicenseType,
                x.Quantity,
                UsedQuantity,
                x.Quantity - UsedQuantity,
                x.StartDate,
                x.EndDate,
                x.IsPerpetual,
                x.RenewalRequired,
                x.Status,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<LicensePackageListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<LicensePackageDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.LicensePackages
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Acquisition)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<LicensePackageOperationResult> CreateAsync(
        CreateLicensePackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidatePackageAsync(
            request.AcquisitionId,
            request.ProductId,
            request.LicenseType,
            request.Quantity,
            request.LicenseAccountEmail,
            request.LicensePortalUrl,
            request.IsPerpetual,
            request.EndDate,
            cancellationToken);
        if (validationError is not null)
        {
            return new LicensePackageOperationResult(false, validationError);
        }

        if (!Enum.IsDefined(request.Status))
        {
            return new LicensePackageOperationResult(false, "License package status is invalid.");
        }

        var now = DateTime.UtcNow;
        var entity = new LicensePackage
        {
            AcquisitionId = request.AcquisitionId,
            ProductId = request.ProductId,
            LicenseType = request.LicenseType,
            Quantity = request.Quantity,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsPerpetual = request.IsPerpetual,
            RenewalRequired = request.RenewalRequired,
            RenewalDate = request.RenewalDate,
            SerialNumber = LicenseManagementValidation.TrimOrNull(request.SerialNumber),
            LicenseKey = LicenseManagementValidation.TrimOrNull(request.LicenseKey),
            LicenseAccountEmail = LicenseManagementValidation.TrimOrNull(request.LicenseAccountEmail),
            LicensePortalUrl = LicenseManagementValidation.TrimOrNull(request.LicensePortalUrl),
            LicenseNotes = LicenseManagementValidation.TrimOrNull(request.LicenseNotes),
            IsActive = request.IsActive,
            Status = request.Status,
            CreatedAt = now,
            CreatedBy = request.ActorUserName
        };

        await context.LicensePackages.AddAsync(entity, cancellationToken);
        await WriteAuditAsync(
            context,
            "Create",
            "LicensePackage",
            entity.Id,
            $"License package created for product {request.ProductId} under acquisition {request.AcquisitionId}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await context.Entry(entity).Reference(x => x.Product).LoadAsync(cancellationToken);
        await context.Entry(entity).Reference(x => x.Acquisition).LoadAsync(cancellationToken);

        return new LicensePackageOperationResult(true, "License package created.", Map(entity));
    }

    public async Task<LicensePackageOperationResult> UpdateAsync(
        UpdateLicensePackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicensePackages
            .Include(x => x.Product)
            .Include(x => x.Acquisition)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicensePackageOperationResult(false, "License package was not found.");
        }

        var validationError = await ValidatePackageAsync(
            request.AcquisitionId,
            request.ProductId,
            request.LicenseType,
            request.Quantity,
            request.LicenseAccountEmail,
            request.LicensePortalUrl,
            request.IsPerpetual,
            request.EndDate,
            cancellationToken);
        if (validationError is not null)
        {
            return new LicensePackageOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        entity.AcquisitionId = request.AcquisitionId;
        entity.ProductId = request.ProductId;
        entity.LicenseType = request.LicenseType;
        entity.Quantity = request.Quantity;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.IsPerpetual = request.IsPerpetual;
        entity.RenewalRequired = request.RenewalRequired;
        entity.RenewalDate = request.RenewalDate;
        entity.SerialNumber = LicenseManagementValidation.TrimOrNull(request.SerialNumber);
        entity.LicenseKey = LicenseManagementValidation.TrimOrNull(request.LicenseKey);
        entity.LicenseAccountEmail = LicenseManagementValidation.TrimOrNull(request.LicenseAccountEmail);
        entity.LicensePortalUrl = LicenseManagementValidation.TrimOrNull(request.LicensePortalUrl);
        entity.LicenseNotes = LicenseManagementValidation.TrimOrNull(request.LicenseNotes);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "Update",
            "LicensePackage",
            entity.Id,
            $"License package updated: {entity.Id}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicensePackageOperationResult(true, "License package updated.", Map(entity));
    }

    public async Task<LicensePackageOperationResult> UpdateStatusAsync(
        UpdateLicensePackageStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicensePackages
            .Include(x => x.Product)
            .Include(x => x.Acquisition)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicensePackageOperationResult(false, "License package was not found.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            return new LicensePackageOperationResult(false, "License package status is invalid.");
        }

        if (entity.Status == request.Status)
        {
            return new LicensePackageOperationResult(true, "License package status is unchanged.", Map(entity));
        }

        var now = DateTime.UtcNow;
        entity.Status = request.Status;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "Update",
            "LicensePackage",
            entity.Id,
            $"License package status changed to {request.Status}: {entity.Id}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicensePackageOperationResult(true, "License package status updated.", Map(entity));
    }

    private async Task<string?> ValidatePackageAsync(
        Guid acquisitionId,
        Guid productId,
        LicenseType licenseType,
        int quantity,
        string? licenseAccountEmail,
        string? licensePortalUrl,
        bool isPerpetual,
        DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(licenseType))
        {
            return "License type is invalid.";
        }

        if (quantity < 1)
        {
            return "License quantity must be at least 1.";
        }

        var acquisitionExists = await context.LicenseAcquisitions.AnyAsync(x => x.Id == acquisitionId, cancellationToken);
        if (!acquisitionExists)
        {
            return "Acquisition was not found.";
        }

        var productExists = await context.LicensedProducts.AnyAsync(x => x.Id == productId, cancellationToken);
        if (!productExists)
        {
            return "Product was not found.";
        }

        if (!LicenseManagementValidation.IsValidEmail(licenseAccountEmail))
        {
            return "License account email format is invalid.";
        }

        if (!LicenseManagementValidation.IsValidUrl(licensePortalUrl))
        {
            return "License portal URL format is invalid.";
        }

        if (!isPerpetual && endDate is not null && endDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            // End date in past is allowed per spec - only contract date range validation required
        }

        return null;
    }

    private static LicensePackageDetail Map(LicensePackage entity) =>
        new(
            entity.Id,
            entity.AcquisitionId,
            entity.Acquisition.Title,
            entity.ProductId,
            entity.Product.Name,
            entity.LicenseType,
            entity.Quantity,
            UsedQuantity,
            entity.Quantity - UsedQuantity,
            entity.StartDate,
            entity.EndDate,
            entity.IsPerpetual,
            entity.RenewalRequired,
            entity.RenewalDate,
            entity.SerialNumber,
            entity.LicenseKey,
            entity.LicenseAccountEmail,
            entity.LicensePortalUrl,
            entity.LicenseNotes,
            entity.IsActive,
            entity.Status,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);
}
