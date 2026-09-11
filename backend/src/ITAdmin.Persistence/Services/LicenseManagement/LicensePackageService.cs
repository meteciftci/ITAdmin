using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using static ITAdmin.Persistence.Services.LicenseManagement.LicenseManagementServiceHelpers;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicensePackageService(AppDbContext context, ISecretProtector secretProtector) : ILicensePackageService
{

    public async Task<PagedResult<LicensePackageListItem>> GetListAsync(
        LicensePackageListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);
        var itemsQuery = context.LicensePackages
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Purchase)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.Product.Name, pattern)
                || EF.Functions.ILike(x.Purchase.Title, pattern)
                || (x.SerialNumber != null && EF.Functions.ILike(x.SerialNumber, pattern)));
        }

        if (query.PurchaseId is { } purchaseId)
        {
            itemsQuery = itemsQuery.Where(x => x.PurchaseId == purchaseId);
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

        var page = await itemsQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                ProductName = x.Product.Name,
                PurchaseTitle = x.Purchase.Title,
                x.LicenseType,
                x.Quantity,
                x.StartDate,
                x.EndDate,
                x.IsPerpetual,
                x.RenewalRequired,
                x.Status,
                x.IsActive,
            })
            .ToListAsync(cancellationToken);

        var usedByPackage = await GetActiveSeatCountsAsync(page.Select(x => x.Id).ToList(), cancellationToken);

        var items = page.Select(x =>
        {
            var used = usedByPackage.GetValueOrDefault(x.Id);
            return new LicensePackageListItem(
                x.Id,
                x.ProductName,
                x.PurchaseTitle,
                x.LicenseType,
                x.Quantity,
                used,
                Math.Max(0, x.Quantity - used),
                x.StartDate,
                x.EndDate,
                x.IsPerpetual,
                x.RenewalRequired,
                x.Status,
                x.IsActive);
        }).ToList();

        return new PagedResult<LicensePackageListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<LicensePackageDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.LicensePackages
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Purchase)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : Map(entity, await GetActiveSeatCountAsync(id, cancellationToken));
    }

    private async Task<Dictionary<Guid, int>> GetActiveSeatCountsAsync(
        IReadOnlyCollection<Guid> packageIds,
        CancellationToken cancellationToken)
    {
        if (packageIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await context.LicenseSeatAssignments
            .Where(s => packageIds.Contains(s.PackageId) && s.Status == LicenseSeatAssignmentStatus.Active)
            .GroupBy(s => s.PackageId)
            .Select(g => new { PackageId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PackageId, x => x.Count, cancellationToken);
    }

    private Task<int> GetActiveSeatCountAsync(Guid packageId, CancellationToken cancellationToken) =>
        context.LicenseSeatAssignments
            .CountAsync(
                s => s.PackageId == packageId && s.Status == LicenseSeatAssignmentStatus.Active,
                cancellationToken);

    public async Task<LicensePackageOperationResult> CreateAsync(
        CreateLicensePackageRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidatePackageAsync(
            request.PurchaseId,
            request.ProductId,
            request.LicenseType,
            request.Quantity,
            request.LicenseAccountEmail,
            request.LicensePortalUrl,
            request.StartDate,
            request.IsPerpetual,
            request.EndDate,
            request.RenewalRequired,
            request.RenewalDate,
            request.IsActive,
            request.Status,
            requireOpenPurchase: true,
            requireActiveProduct: true,
            cancellationToken);
        if (validationError is not null)
        {
            return new LicensePackageOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        var entity = new LicensePackage
        {
            PurchaseId = request.PurchaseId,
            ProductId = request.ProductId,
            LicenseType = request.LicenseType,
            Quantity = request.Quantity,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsPerpetual = request.IsPerpetual,
            RenewalRequired = request.RenewalRequired,
            RenewalDate = request.RenewalDate,
            SerialNumber = LicenseManagementValidation.TrimOrNull(request.SerialNumber),
            LicenseKey = ProtectLicenseKey(request.LicenseKey),
            LicenseKeyIsEncrypted = !string.IsNullOrWhiteSpace(request.LicenseKey),
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
            $"License package created for product {request.ProductId} under purchase {request.PurchaseId}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await context.Entry(entity).Reference(x => x.Product).LoadAsync(cancellationToken);
        await context.Entry(entity).Reference(x => x.Purchase).LoadAsync(cancellationToken);

        return new LicensePackageOperationResult(true, "License package created.", Map(entity, 0));
    }

    public async Task<LicensePackageOperationResult> UpdateAsync(
        UpdateLicensePackageRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginMutationTransactionAsync(context, cancellationToken);
        await LockLicensePackagesAsync(context, [request.Id], cancellationToken);
        var entity = await context.LicensePackages
            .Include(x => x.Product)
            .Include(x => x.Purchase)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicensePackageOperationResult(false, "License package was not found.");
        }

        var validationError = await ValidatePackageAsync(
            request.PurchaseId,
            request.ProductId,
            request.LicenseType,
            request.Quantity,
            request.LicenseAccountEmail,
            request.LicensePortalUrl,
            request.StartDate,
            request.IsPerpetual,
            request.EndDate,
            request.RenewalRequired,
            request.RenewalDate,
            request.IsActive,
            request.Status,
            requireOpenPurchase: request.PurchaseId != entity.PurchaseId,
            requireActiveProduct: request.ProductId != entity.ProductId,
            cancellationToken);
        if (validationError is not null)
        {
            return new LicensePackageOperationResult(false, validationError);
        }

        if (!LicenseManagementLifecycleRules.IsPackageTransitionAllowed(entity.Status, request.Status))
        {
            return new LicensePackageOperationResult(
                false,
                $"License package status cannot change from {entity.Status} to {request.Status}.");
        }

        var activeSeatCount = await GetActiveSeatCountAsync(entity.Id, cancellationToken);
        if (request.Quantity < activeSeatCount)
        {
            return new LicensePackageOperationResult(
                false,
                $"License quantity cannot be lower than the active seat count ({activeSeatCount}).");
        }

        var hasOperationalHistory = entity.PreviousPackageId is not null
            || await context.LicenseRequestItemFulfillments.AnyAsync(
                x => x.PackageId == entity.Id, cancellationToken)
            || await context.LicenseSeatAssignments.AnyAsync(
                x => x.PackageId == entity.Id, cancellationToken)
            || await context.LicensePackages.AnyAsync(
                x => x.PreviousPackageId == entity.Id, cancellationToken);
        if (hasOperationalHistory
            && (request.PurchaseId != entity.PurchaseId
                || request.ProductId != entity.ProductId
                || request.LicenseType != entity.LicenseType))
        {
            return new LicensePackageOperationResult(
                false,
                "Purchase, product, and license type cannot change after a package has operational history.");
        }

        var previousStatus = entity.Status;
        var releasedSeatCount = await CloseActiveSeatsForTerminalStatusAsync(
            entity.Id, request.Status, request.ActorUserName, cancellationToken);

        var now = DateTime.UtcNow;
        entity.PurchaseId = request.PurchaseId;
        entity.ProductId = request.ProductId;
        entity.LicenseType = request.LicenseType;
        entity.Quantity = request.Quantity;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.IsPerpetual = request.IsPerpetual;
        entity.RenewalRequired = request.RenewalRequired;
        entity.RenewalDate = request.RenewalDate;
        entity.SerialNumber = LicenseManagementValidation.TrimOrNull(request.SerialNumber);
        entity.LicenseKey = ProtectLicenseKey(request.LicenseKey);
        entity.LicenseKeyIsEncrypted = !string.IsNullOrWhiteSpace(request.LicenseKey);
        entity.LicenseAccountEmail = LicenseManagementValidation.TrimOrNull(request.LicenseAccountEmail);
        entity.LicensePortalUrl = LicenseManagementValidation.TrimOrNull(request.LicensePortalUrl);
        entity.LicenseNotes = LicenseManagementValidation.TrimOrNull(request.LicenseNotes);
        entity.IsActive = LicenseManagementLifecycleRules.IsPackageActive(request.Status);
        entity.Status = request.Status;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "Update",
            "LicensePackage",
            entity.Id,
            previousStatus == entity.Status
                ? $"License package updated: {entity.Id}; closed {releasedSeatCount} active seat(s)."
                : $"License package updated: {entity.Id}; status changed from {previousStatus} to {entity.Status}; "
                    + $"closed {releasedSeatCount} active seat(s).",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        return new LicensePackageOperationResult(
            true,
            "License package updated.",
            Map(entity, activeSeatCount - releasedSeatCount));
    }

    public async Task<LicensePackageOperationResult> UpdateStatusAsync(
        UpdateLicensePackageStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginMutationTransactionAsync(context, cancellationToken);
        await LockLicensePackagesAsync(context, [request.Id], cancellationToken);
        var entity = await context.LicensePackages
            .Include(x => x.Product)
            .Include(x => x.Purchase)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicensePackageOperationResult(false, "License package was not found.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            return new LicensePackageOperationResult(false, "License package status is invalid.");
        }

        if (!LicenseManagementLifecycleRules.IsPackageTransitionAllowed(entity.Status, request.Status))
        {
            return new LicensePackageOperationResult(
                false,
                $"License package status cannot change from {entity.Status} to {request.Status}.");
        }

        if (entity.Status == request.Status)
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new LicensePackageOperationResult(
                true,
                "License package status is unchanged.",
                Map(entity, await GetActiveSeatCountAsync(entity.Id, cancellationToken)));
        }

        var previousStatus = entity.Status;
        var now = DateTime.UtcNow;
        var releasedSeatCount = await CloseActiveSeatsForTerminalStatusAsync(
            entity.Id, request.Status, request.ActorUserName, cancellationToken);
        entity.Status = request.Status;
        entity.IsActive = LicenseManagementLifecycleRules.IsPackageActive(request.Status);
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "StatusChange",
            "LicensePackage",
            entity.Id,
            $"License package status changed from {previousStatus} to {request.Status}: {entity.Id}; "
            + $"closed {releasedSeatCount} active seat(s).",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        return new LicensePackageOperationResult(
            true,
            "License package status updated.",
            Map(entity, await GetActiveSeatCountAsync(entity.Id, cancellationToken)));
    }

    private async Task<string?> ValidatePackageAsync(
        Guid purchaseId,
        Guid productId,
        LicenseType licenseType,
        int quantity,
        string? licenseAccountEmail,
        string? licensePortalUrl,
        DateOnly? startDate,
        bool isPerpetual,
        DateOnly? endDate,
        bool renewalRequired,
        DateOnly? renewalDate,
        bool isActive,
        LicensePackageStatus status,
        bool requireOpenPurchase,
        bool requireActiveProduct,
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

        if (!Enum.IsDefined(status))
        {
            return "License package status is invalid.";
        }

        if (isActive != LicenseManagementLifecycleRules.IsPackageActive(status))
        {
            return "License package active flag must match its status.";
        }

        var purchaseStatus = await context.LicensePurchases
            .Where(x => x.Id == purchaseId)
            .Select(x => (LicensePurchaseStatus?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (purchaseStatus is null)
        {
            return "Purchase was not found.";
        }

        if (requireOpenPurchase
            && !LicenseManagementLifecycleRules.IsPurchaseOpenForPackages(purchaseStatus.Value))
        {
            return "License packages can only be attached to draft or active purchases.";
        }

        var productIsActive = await context.LicensedProducts
            .Where(x => x.Id == productId)
            .Select(x => (bool?)x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
        if (productIsActive is null)
        {
            return "Product was not found.";
        }

        if (requireActiveProduct && !productIsActive.Value)
        {
            return "A passive product cannot be used by a license package.";
        }

        if (!LicenseManagementValidation.IsValidEmail(licenseAccountEmail))
        {
            return "License account email format is invalid.";
        }

        if (!LicenseManagementValidation.IsValidUrl(licensePortalUrl))
        {
            return "License portal URL format is invalid.";
        }

        var dateValidationError = LicenseManagementLifecycleRules.ValidatePackageDates(
            startDate, endDate, isPerpetual, renewalRequired, renewalDate);
        if (dateValidationError is not null)
        {
            return dateValidationError;
        }

        return null;
    }

    private async Task<int> CloseActiveSeatsForTerminalStatusAsync(
        Guid packageId,
        LicensePackageStatus targetStatus,
        string? actorUserName,
        CancellationToken cancellationToken)
    {
        if (targetStatus is not (LicensePackageStatus.Expired
            or LicensePackageStatus.Cancelled
            or LicensePackageStatus.Archived))
        {
            return 0;
        }

        var seats = await context.LicenseSeatAssignments
            .Where(x => x.PackageId == packageId && x.Status == LicenseSeatAssignmentStatus.Active)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var seat in seats)
        {
            seat.Status = LicenseSeatAssignmentStatus.Released;
            seat.ReleasedDate = DateOnly.FromDateTime(now);
            seat.UpdatedAt = now;
            seat.UpdatedBy = actorUserName;
        }

        return seats.Count;
    }

    private string? ProtectLicenseKey(string? licenseKey)
    {
        var normalized = LicenseManagementValidation.TrimOrNull(licenseKey);
        return normalized is null ? null : secretProtector.Protect(normalized);
    }

    private string? UnprotectLicenseKey(LicensePackage entity) =>
        entity.LicenseKey is null
            ? null
            : entity.LicenseKeyIsEncrypted
                ? secretProtector.Unprotect(entity.LicenseKey)
                : entity.LicenseKey;

    private LicensePackageDetail Map(LicensePackage entity, int usedQuantity) =>
        new(
            entity.Id,
            entity.PurchaseId,
            entity.Purchase.Title,
            entity.ProductId,
            entity.Product.Name,
            entity.LicenseType,
            entity.Quantity,
            usedQuantity,
            Math.Max(0, entity.Quantity - usedQuantity),
            entity.StartDate,
            entity.EndDate,
            entity.IsPerpetual,
            entity.RenewalRequired,
            entity.RenewalDate,
            entity.SerialNumber,
            UnprotectLicenseKey(entity),
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
