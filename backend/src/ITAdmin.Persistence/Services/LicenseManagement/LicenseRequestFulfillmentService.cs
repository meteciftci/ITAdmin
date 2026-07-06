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

public sealed class LicenseRequestFulfillmentService(AppDbContext context) : ILicenseRequestFulfillmentService
{
    public async Task<PagedResult<LicenseFulfillmentCandidateItem>> GetCandidatesAsync(
        LicenseFulfillmentCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);

        // Open lines that still need triage or fulfillment. Pending/InReview items are shown so they
        // can be triaged in place; Approved/PartiallyFulfilled items with remaining quantity can be
        // fulfilled. Terminal items (Fulfilled/Rejected/Cancelled) are excluded.
        var itemsQuery = context.LicenseRequestItems
            .AsNoTracking()
            .Where(x => x.Request.IsActive
                && (x.Status == LicenseRequestItemStatus.Pending
                    || x.Status == LicenseRequestItemStatus.InReview
                    || ((x.Status == LicenseRequestItemStatus.Approved
                            || x.Status == LicenseRequestItemStatus.PartiallyFulfilled)
                        && (x.ApprovedQuantity ?? 0) > x.FulfilledQuantity)));

        if (query.ProductId is { } productId)
        {
            itemsQuery = itemsQuery.Where(x => x.ProductId == productId);
        }

        if (!string.IsNullOrWhiteSpace(query.RequesterUnitObjectGuid))
        {
            itemsQuery = itemsQuery.Where(x => x.Request.RequesterUnitObjectGuid == query.RequesterUnitObjectGuid);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.Product.Name, pattern)
                || EF.Functions.ILike(x.Request.RequesterUnitDisplayName, pattern));
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await itemsQuery
            .OrderBy(x => x.Request.RequestDate)
            .ThenBy(x => x.Product.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LicenseFulfillmentCandidateItem(
                x.RequestId,
                x.Id,
                x.Request.RequestSource,
                x.Request.RequestDate,
                x.Request.RequesterUnitDisplayName,
                x.ProductId,
                x.Product.Name,
                x.Product.Brand,
                x.RequestedQuantity,
                x.ApprovedQuantity,
                x.FulfilledQuantity,
                (x.ApprovedQuantity ?? 0) - x.FulfilledQuantity,
                x.Status,
                (x.Status == LicenseRequestItemStatus.Approved
                        || x.Status == LicenseRequestItemStatus.PartiallyFulfilled)
                    && (x.ApprovedQuantity ?? 0) > x.FulfilledQuantity))
            .ToListAsync(cancellationToken);

        return new PagedResult<LicenseFulfillmentCandidateItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<LicenseRequestOperationResult> TriageAsync(
        TriageLicenseRequestItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            return new LicenseRequestOperationResult(false, "No request items were provided for triage.");
        }

        var ids = request.Items.Select(x => x.RequestItemId).Distinct().ToList();
        var items = await context.LicenseRequestItems
            .Include(x => x.Request)
            .Where(x => ids.Contains(x.Id) && x.Request.IsActive)
            .ToListAsync(cancellationToken);

        if (items.Count != ids.Count)
        {
            return new LicenseRequestOperationResult(false, "Some request items were not found.");
        }

        var now = DateTime.UtcNow;
        foreach (var input in request.Items)
        {
            var item = items.First(x => x.Id == input.RequestItemId);

            if (!LicenseRequestRules.ManualItemStatuses.Contains(input.Status))
            {
                return new LicenseRequestOperationResult(false, "Selected item status cannot be set manually.");
            }

            if (item.FulfilledQuantity > 0)
            {
                return new LicenseRequestOperationResult(
                    false,
                    "A partially or fully fulfilled item cannot be re-triaged.");
            }

            if (input.Status == LicenseRequestItemStatus.Approved)
            {
                var approved = input.ApprovedQuantity ?? item.RequestedQuantity;
                if (approved < 1 || approved > item.RequestedQuantity)
                {
                    return new LicenseRequestOperationResult(
                        false,
                        "Approved quantity must be between 1 and the requested quantity.");
                }

                item.ApprovedQuantity = approved;
            }
            else
            {
                item.ApprovedQuantity = null;
            }

            item.Status = input.Status;
            item.UpdatedAt = now;
            item.UpdatedBy = request.ActorUserName;
        }

        var affectedRequestIds = items.Select(x => x.RequestId).Distinct().ToList();
        await DeriveAndApplyRequestStatusesAsync(affectedRequestIds, now, request.ActorUserName, cancellationToken);

        foreach (var requestId in affectedRequestIds)
        {
            await WriteAuditAsync(
                context,
                "Triage",
                "LicenseRequest",
                requestId,
                $"License request items triaged ({items.Count(x => x.RequestId == requestId)} item(s)).",
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return new LicenseRequestOperationResult(true, "License request items triaged.");
    }

    public async Task<LicenseFulfillmentResult> ConvertToPurchaseAsync(
        ConvertLicenseRequestItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
        {
            return new LicenseFulfillmentResult(false, "No lines were provided for conversion.");
        }

        if ((request.ExistingPurchaseId is null) == (request.NewPurchase is null))
        {
            return new LicenseFulfillmentResult(
                false,
                "Provide exactly one target: an existing purchase or a new purchase.");
        }

        var lineIds = request.Lines.Select(x => x.RequestItemId).ToList();
        if (lineIds.Count != lineIds.Distinct().Count())
        {
            return new LicenseFulfillmentResult(false, "A request item was listed more than once.");
        }

        var items = await context.LicenseRequestItems
            .Include(x => x.Product)
            .Where(x => lineIds.Contains(x.Id) && x.Request.IsActive)
            .ToListAsync(cancellationToken);

        if (items.Count != lineIds.Count)
        {
            return new LicenseFulfillmentResult(false, "Some request items were not found.");
        }

        // Validate every line before any write.
        foreach (var line in request.Lines)
        {
            var item = items.First(x => x.Id == line.RequestItemId);

            if (item.Status is not (LicenseRequestItemStatus.Approved or LicenseRequestItemStatus.PartiallyFulfilled))
            {
                return new LicenseFulfillmentResult(false, "Only approved request items can be fulfilled.");
            }

            if (!item.Product.IsActive)
            {
                return new LicenseFulfillmentResult(false, "A passive product cannot be fulfilled.");
            }

            var remaining = (item.ApprovedQuantity ?? 0) - item.FulfilledQuantity;
            if (line.FulfillQuantity < 1 || line.FulfillQuantity > remaining)
            {
                return new LicenseFulfillmentResult(
                    false,
                    "Fulfill quantity must be between 1 and the remaining approved quantity.");
            }
        }

        var productGroups = request.Lines
            .GroupBy(line => items.First(x => x.Id == line.RequestItemId).ProductId)
            .ToList();

        var defaultsByProduct = request.PackageDefaults.ToDictionary(x => x.ProductId);
        foreach (var group in productGroups)
        {
            if (!defaultsByProduct.ContainsKey(group.Key))
            {
                return new LicenseFulfillmentResult(false, "License package settings are required for each product.");
            }
        }

        var now = DateTime.UtcNow;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Phase 1: target purchase.
        LicensePurchase purchase;
        if (request.ExistingPurchaseId is { } existingPurchaseId)
        {
            var existing = await context.LicensePurchases
                .FirstOrDefaultAsync(x => x.Id == existingPurchaseId, cancellationToken);
            if (existing is null)
            {
                return new LicenseFulfillmentResult(false, "Target purchase was not found.");
            }

            if (existing.Status is not (LicensePurchaseStatus.Draft or LicensePurchaseStatus.Active))
            {
                return new LicenseFulfillmentResult(
                    false,
                    "Only draft or active purchases can receive fulfillment packages.");
            }

            purchase = existing;
        }
        else
        {
            var input = request.NewPurchase!;
            if (string.IsNullOrWhiteSpace(input.Title))
            {
                return new LicenseFulfillmentResult(false, "Purchase title is required.");
            }

            purchase = new LicensePurchase
            {
                PurchaseType = input.PurchaseType,
                Title = input.Title.Trim(),
                Description = LicenseManagementValidation.TrimOrNull(input.Description),
                PurchaseDate = input.PurchaseDate,
                SupplierCompanyId = input.SupplierCompanyId,
                SupportCompanyId = input.SupportCompanyId,
                ActualTotalCost = input.ActualTotalCost,
                Currency = LicenseManagementValidation.TrimOrNull(input.Currency),
                VatIncluded = input.VatIncluded,
                Notes = LicenseManagementValidation.TrimOrNull(input.Notes),
                Status = LicensePurchaseStatus.Draft,
                CreatedAt = now,
                CreatedBy = request.ActorUserName,
            };
            await context.LicensePurchases.AddAsync(purchase, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Phase 2: one package per product (quantity = sum of that product's fulfill quantities).
        var packageByProduct = new Dictionary<Guid, LicensePackage>();
        foreach (var group in productGroups)
        {
            var defaults = defaultsByProduct[group.Key];
            var quantity = group.Sum(line => line.FulfillQuantity);

            var package = new LicensePackage
            {
                PurchaseId = purchase.Id,
                ProductId = group.Key,
                LicenseType = defaults.LicenseType,
                Quantity = quantity,
                StartDate = defaults.StartDate,
                EndDate = defaults.EndDate,
                IsPerpetual = defaults.IsPerpetual,
                IsActive = true,
                Status = LicensePackageStatus.Active,
                CreatedAt = now,
                CreatedBy = request.ActorUserName,
            };
            await context.LicensePackages.AddAsync(package, cancellationToken);
            packageByProduct[group.Key] = package;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Phase 3: fulfillment links + item/request status derivation.
        foreach (var line in request.Lines)
        {
            var item = items.First(x => x.Id == line.RequestItemId);
            var package = packageByProduct[item.ProductId];

            var fulfillment = new LicenseRequestItemFulfillment
            {
                RequestItemId = item.Id,
                PackageId = package.Id,
                Quantity = line.FulfillQuantity,
                CreatedAt = now,
                CreatedBy = request.ActorUserName,
            };
            await context.LicenseRequestItemFulfillments.AddAsync(fulfillment, cancellationToken);

            item.FulfilledQuantity += line.FulfillQuantity;
            item.Status = LicenseRequestRules.DeriveItemStatus(item.ApprovedQuantity, item.FulfilledQuantity);
            item.UpdatedAt = now;
            item.UpdatedBy = request.ActorUserName;
        }

        await DeriveAndApplyRequestStatusesAsync(items.Select(x => x.RequestId).Distinct(), now, request.ActorUserName, cancellationToken);

        await WriteAuditAsync(
            context,
            "Create",
            "LicensePurchase",
            purchase.Id,
            $"License purchase received {packageByProduct.Count} fulfillment package(s) from {request.Lines.Count} request line(s).",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);

        foreach (var requestId in items.Select(x => x.RequestId).Distinct())
        {
            await WriteAuditAsync(
                context,
                "Fulfill",
                "LicenseRequest",
                requestId,
                $"License request fulfilled into purchase {purchase.Id}.",
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new LicenseFulfillmentResult(
            true,
            "License requests converted into a purchase.",
            purchase.Id,
            packageByProduct.Values.Select(x => x.Id).ToList());
    }

    private async Task DeriveAndApplyRequestStatusesAsync(
        IEnumerable<Guid> requestIds,
        DateTime now,
        string? actorUserName,
        CancellationToken cancellationToken)
    {
        var ids = requestIds.ToList();
        var requests = await context.LicenseRequests
            .Include(x => x.Items)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var request in requests)
        {
            var derived = LicenseRequestRules.DeriveRequestStatus(request.Items.Select(x => x.Status));
            if (request.Status != derived)
            {
                request.Status = derived;
                request.UpdatedAt = now;
                request.UpdatedBy = actorUserName;
            }
        }
    }
}
