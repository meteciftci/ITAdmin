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
        var renewalLines = request.RenewalLines ?? [];
        var manualLines = request.ManualLines ?? [];

        if (request.Lines.Count == 0 && renewalLines.Count == 0 && manualLines.Count == 0)
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

        // Validate renewal lines.
        var renewalSourceIds = renewalLines.Select(x => x.SourcePackageId).Distinct().ToList();
        var renewalSources = await context.LicensePackages
            .Where(x => renewalSourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var line in renewalLines)
        {
            if (!renewalSources.ContainsKey(line.SourcePackageId))
            {
                return new LicenseFulfillmentResult(false, "A renewal source package was not found.");
            }

            if (line.Quantity < 1)
            {
                return new LicenseFulfillmentResult(false, "Renewal quantity must be at least 1.");
            }

            if (line.LicenseType is { } renewalType && !Enum.IsDefined(renewalType))
            {
                return new LicenseFulfillmentResult(false, "Renewal license type is invalid.");
            }
        }

        // Validate manual lines.
        var manualProductIds = manualLines.Select(x => x.ProductId).Distinct().ToList();
        var manualProducts = await context.LicensedProducts
            .Where(x => manualProductIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var line in manualLines)
        {
            if (!manualProducts.TryGetValue(line.ProductId, out var manualProduct))
            {
                return new LicenseFulfillmentResult(false, "A manual line product was not found.");
            }

            if (!manualProduct.IsActive)
            {
                return new LicenseFulfillmentResult(false, "A passive product cannot be added.");
            }

            if (line.Quantity < 1)
            {
                return new LicenseFulfillmentResult(false, "Manual line quantity must be at least 1.");
            }

            if (!Enum.IsDefined(line.LicenseType))
            {
                return new LicenseFulfillmentResult(false, "Manual line license type is invalid.");
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

        var createdPackages = new List<LicensePackage>(packageByProduct.Values);

        // Phase 2b: renewal packages (copy config from the source, optionally expire it and carry seats over).
        var renewalPackages = new List<(ConvertFulfillmentRenewalLineInput Line, LicensePackage Source, LicensePackage New)>();
        foreach (var line in renewalLines)
        {
            var source = renewalSources[line.SourcePackageId];
            var renewal = new LicensePackage
            {
                PurchaseId = purchase.Id,
                ProductId = source.ProductId,
                LicenseType = line.LicenseType ?? source.LicenseType,
                Quantity = line.Quantity,
                StartDate = line.StartDate,
                EndDate = line.EndDate,
                IsPerpetual = line.IsPerpetual,
                RenewalRequired = source.RenewalRequired,
                SerialNumber = source.SerialNumber,
                LicenseKey = source.LicenseKey,
                LicenseAccountEmail = source.LicenseAccountEmail,
                LicensePortalUrl = source.LicensePortalUrl,
                LicenseNotes = source.LicenseNotes,
                PreviousPackageId = source.Id,
                IsActive = true,
                Status = LicensePackageStatus.Active,
                CreatedAt = now,
                CreatedBy = request.ActorUserName,
            };
            await context.LicensePackages.AddAsync(renewal, cancellationToken);
            renewalPackages.Add((line, source, renewal));
            createdPackages.Add(renewal);

            if (line.ExpireSourcePackage)
            {
                source.Status = LicensePackageStatus.Expired;
                source.IsActive = false;
                source.RenewalRequired = false;
                source.UpdatedAt = now;
                source.UpdatedBy = request.ActorUserName;
            }
        }

        // Phase 2c: manual packages (no request line, no renewal link).
        foreach (var line in manualLines)
        {
            var manual = new LicensePackage
            {
                PurchaseId = purchase.Id,
                ProductId = line.ProductId,
                LicenseType = line.LicenseType,
                Quantity = line.Quantity,
                StartDate = line.StartDate,
                EndDate = line.EndDate,
                IsPerpetual = line.IsPerpetual,
                IsActive = true,
                Status = LicensePackageStatus.Active,
                CreatedAt = now,
                CreatedBy = request.ActorUserName,
            };
            await context.LicensePackages.AddAsync(manual, cancellationToken);
            createdPackages.Add(manual);
        }

        if (renewalPackages.Count > 0 || manualLines.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        // Phase 2d: carry active seat assignments from each renewed source onto its new package.
        foreach (var (line, source, renewal) in renewalPackages)
        {
            if (!line.CopySeatAssignments)
            {
                continue;
            }

            var activeSeats = await context.LicenseSeatAssignments
                .Where(x => x.PackageId == source.Id && x.Status == LicenseSeatAssignmentStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var seat in activeSeats)
            {
                await context.LicenseSeatAssignments.AddAsync(
                    new LicenseSeatAssignment
                    {
                        PackageId = renewal.Id,
                        AdObjectId = seat.AdObjectId,
                        DisplayName = seat.DisplayName,
                        SamAccountName = seat.SamAccountName,
                        UserPrincipalName = seat.UserPrincipalName,
                        Mail = seat.Mail,
                        NationalId = seat.NationalId,
                        Department = seat.Department,
                        Title = seat.Title,
                        AssignedDate = DateOnly.FromDateTime(now),
                        Status = LicenseSeatAssignmentStatus.Active,
                        ReplacesAssignmentId = seat.Id,
                        SourceRequestItemId = seat.SourceRequestItemId,
                        Note = "Carried over on renewal.",
                        CreatedAt = now,
                        CreatedBy = request.ActorUserName,
                    },
                    cancellationToken);

                seat.Status = LicenseSeatAssignmentStatus.Transferred;
                seat.ReleasedDate = DateOnly.FromDateTime(now);
                seat.UpdatedAt = now;
                seat.UpdatedBy = request.ActorUserName;
            }

            await WriteAuditAsync(
                context,
                "Renew",
                "LicensePackage",
                renewal.Id,
                $"Package renewed from {source.Id}; carried {activeSeats.Count} seat assignment(s).",
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent,
                cancellationToken);
        }

        foreach (var (_, source, renewal) in renewalPackages.Where(x => !x.Line.CopySeatAssignments))
        {
            await WriteAuditAsync(
                context,
                "Renew",
                "LicensePackage",
                renewal.Id,
                $"Package renewed from {source.Id}.",
                request.ActorUserId,
                request.ActorUserName,
                request.ActorIpAddress,
                request.ActorUserAgent,
                cancellationToken);
        }

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
            $"License purchase received {createdPackages.Count} package(s): "
            + $"{packageByProduct.Count} from {request.Lines.Count} request line(s), "
            + $"{renewalPackages.Count} renewal(s), {manualLines.Count} manual.",
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
            createdPackages.Select(x => x.Id).ToList());
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
