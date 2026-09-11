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
            .ThenBy(x => x.Id)
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
                x.LicenseType,
                x.RequestedQuantity,
                x.ApprovedQuantity,
                x.FulfilledQuantity,
                (x.ApprovedQuantity ?? 0) - x.FulfilledQuantity,
                x.Status,
                (x.Status == LicenseRequestItemStatus.Approved
                        || x.Status == LicenseRequestItemStatus.PartiallyFulfilled)
                    && (x.ApprovedQuantity ?? 0) > x.FulfilledQuantity,
                x.Users
                    .OrderBy(user => user.CreatedAt)
                    .ThenBy(user => user.Id)
                    .Select(user => new LicenseFulfillmentCandidateUser(
                        user.Id,
                        user.AdObjectId,
                        user.SamAccountName,
                        user.UserPrincipalName,
                        user.DisplayName,
                        user.Department,
                        user.Title,
                        user.Mail,
                        user.Status))
                    .ToList()))
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
        await using var transaction = await BeginMutationTransactionAsync(context, cancellationToken);
        await LockLicenseRequestItemsAsync(context, ids, cancellationToken);
        var items = await context.LicenseRequestItems
            .Include(x => x.Request)
            .Include(x => x.Users)
            .Where(x => ids.Contains(x.Id) && x.Request.IsActive)
            .ToListAsync(cancellationToken);

        if (request.Items.Count != ids.Count)
        {
            return new LicenseRequestOperationResult(false, "A request item was listed more than once.");
        }

        if (items.Count != ids.Count)
        {
            return new LicenseRequestOperationResult(false, "Some request items were not found.");
        }

        var approvedQuantities = new Dictionary<Guid, int>();
        var approvedUsersByItem = new Dictionary<Guid, HashSet<Guid>>();
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

                approvedQuantities[item.Id] = approved;

                if (item.LicenseType == LicenseType.NamedUser)
                {
                    var approvedUserIds = input.ApprovedUserIds?.Distinct().ToHashSet()
                        ?? item.Users
                            .OrderBy(x => x.CreatedAt)
                            .ThenBy(x => x.Id)
                            .Take(approved)
                            .Select(x => x.Id)
                            .ToHashSet();

                    if (approvedUserIds.Count != approved
                        || approvedUserIds.Any(id => item.Users.All(user => user.Id != id)))
                    {
                        return new LicenseRequestOperationResult(
                            false,
                            "Approved named users must belong to the request item and match the approved quantity.");
                    }

                    approvedUsersByItem[item.Id] = approvedUserIds;
                }
                else if (input.ApprovedUserIds is { Count: > 0 })
                {
                    return new LicenseRequestOperationResult(
                        false,
                        "Users cannot be approved for a license type that is not user-bound.");
                }
            }
            else
            {
                if (input.ApprovedUserIds is { Count: > 0 })
                {
                    return new LicenseRequestOperationResult(
                        false,
                        "Approved users can only be provided while approving an item.");
                }

            }
        }

        // Apply only after the complete batch has passed validation.
        var now = DateTime.UtcNow;
        foreach (var input in request.Items)
        {
            var item = items.First(x => x.Id == input.RequestItemId);
            item.ApprovedQuantity = input.Status == LicenseRequestItemStatus.Approved
                ? approvedQuantities[item.Id]
                : null;

            if (item.LicenseType == LicenseType.NamedUser)
            {
                var userStatus = input.Status switch
                {
                    LicenseRequestItemStatus.Approved => LicenseRequestItemUserStatus.Approved,
                    LicenseRequestItemStatus.Rejected => LicenseRequestItemUserStatus.Rejected,
                    LicenseRequestItemStatus.Cancelled => LicenseRequestItemUserStatus.Cancelled,
                    _ => LicenseRequestItemUserStatus.Pending,
                };
                approvedUsersByItem.TryGetValue(item.Id, out var approvedUserIds);
                foreach (var user in item.Users)
                {
                    user.Status = input.Status == LicenseRequestItemStatus.Approved
                        && approvedUserIds is not null
                        && !approvedUserIds.Contains(user.Id)
                            ? LicenseRequestItemUserStatus.Rejected
                            : userStatus;
                    user.UpdatedAt = now;
                    user.UpdatedBy = request.ActorUserName;
                }
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
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
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

        await using var transaction = await BeginMutationTransactionAsync(context, cancellationToken);
        await LockLicenseRequestItemsAsync(context, lineIds, cancellationToken);
        var items = await context.LicenseRequestItems
            .Include(x => x.Product)
            .Include(x => x.Users)
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

            var selectedUserIds = line.RequestItemUserIds?.Distinct().ToHashSet() ?? [];
            if (item.LicenseType == LicenseType.NamedUser)
            {
                if (selectedUserIds.Count != line.FulfillQuantity)
                {
                    return new LicenseFulfillmentResult(
                        false,
                        "Named-user fulfillment must identify one approved request user per fulfilled license.");
                }

                if (selectedUserIds.Any(id => item.Users.All(user => user.Id != id))
                    || item.Users.Any(user => selectedUserIds.Contains(user.Id)
                        && user.Status != LicenseRequestItemUserStatus.Approved))
                {
                    return new LicenseFulfillmentResult(
                        false,
                        "Only approved users belonging to the request item can be fulfilled.");
                }
            }
            else if (selectedUserIds.Count > 0)
            {
                return new LicenseFulfillmentResult(
                    false,
                    "Request users cannot be supplied for a license type that is not user-bound.");
            }
        }

        var duplicateNamedUser = request.Lines
            .SelectMany(line =>
            {
                var item = items.First(x => x.Id == line.RequestItemId);
                var selectedIds = line.RequestItemUserIds?.ToHashSet() ?? [];
                return item.Users
                    .Where(user => selectedIds.Contains(user.Id))
                    .Select(user => new
                    {
                        item.ProductId,
                        item.LicenseType,
                        AdObjectId = user.AdObjectId.Trim().ToUpperInvariant(),
                    });
            })
            .GroupBy(x => (x.ProductId, x.LicenseType, x.AdObjectId))
            .Any(group => group.Count() > 1);
        if (duplicateNamedUser)
        {
            return new LicenseFulfillmentResult(
                false,
                "The same named user cannot consume more than one seat in the same license package.");
        }

        var productGroups = request.Lines
            .GroupBy(line =>
            {
                var item = items.First(x => x.Id == line.RequestItemId);
                return (item.ProductId, item.LicenseType);
            })
            .ToList();

        var duplicateDefaults = request.PackageDefaults
            .GroupBy(x => (x.ProductId, x.LicenseType))
            .Any(x => x.Count() > 1);
        if (duplicateDefaults)
        {
            return new LicenseFulfillmentResult(false, "License package settings were provided more than once for the same product and license type.");
        }

        var defaultsByProductAndType = request.PackageDefaults
            .ToDictionary(x => (x.ProductId, x.LicenseType));
        foreach (var group in productGroups)
        {
            if (!defaultsByProductAndType.ContainsKey(group.Key))
            {
                return new LicenseFulfillmentResult(
                    false,
                    "License package settings are required for each product and requested license type.");
            }

            var defaults = defaultsByProductAndType[group.Key];
            var dateError = LicenseManagementLifecycleRules.ValidatePackageDates(
                defaults.StartDate, defaults.EndDate, defaults.IsPerpetual, false, null);
            if (dateError is not null)
            {
                return new LicenseFulfillmentResult(false, dateError);
            }
        }

        // Validate renewal lines.
        var renewalSourceIds = renewalLines.Select(x => x.SourcePackageId).Distinct().ToList();
        if (renewalSourceIds.Count != renewalLines.Count)
        {
            return new LicenseFulfillmentResult(false, "A source package can only be renewed once per conversion.");
        }

        await LockLicensePackagesAsync(context, renewalSourceIds, cancellationToken);
        var renewalSources = await context.LicensePackages
            .Where(x => renewalSourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var renewalProductIds = renewalSources.Values.Select(x => x.ProductId).Distinct().ToList();
        var activeRenewalProductIds = await context.LicensedProducts
            .Where(x => renewalProductIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var alreadyRenewedSourceIds = await context.LicensePackages
            .Where(x => x.PreviousPackageId != null && renewalSourceIds.Contains(x.PreviousPackageId.Value))
            .Select(x => x.PreviousPackageId!.Value)
            .ToListAsync(cancellationToken);
        var activeRenewalSeatCounts = await context.LicenseSeatAssignments
            .Where(x => renewalSourceIds.Contains(x.PackageId)
                && x.Status == LicenseSeatAssignmentStatus.Active)
            .GroupBy(x => x.PackageId)
            .Select(x => new { PackageId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.PackageId, x => x.Count, cancellationToken);
        foreach (var line in renewalLines)
        {
            if (!renewalSources.ContainsKey(line.SourcePackageId))
            {
                return new LicenseFulfillmentResult(false, "A renewal source package was not found.");
            }

            var source = renewalSources[line.SourcePackageId];
            if (!activeRenewalProductIds.Contains(source.ProductId))
            {
                return new LicenseFulfillmentResult(false, "A passive product cannot be renewed.");
            }

            if (source.Status is LicensePackageStatus.Cancelled or LicensePackageStatus.Archived)
            {
                return new LicenseFulfillmentResult(false, "A cancelled or archived package cannot be renewed.");
            }

            if (alreadyRenewedSourceIds.Contains(source.Id))
            {
                return new LicenseFulfillmentResult(false, "The source package has already been renewed.");
            }

            if ((source.Status is LicensePackageStatus.Active or LicensePackageStatus.Suspended)
                && !line.ExpireSourcePackage)
            {
                return new LicenseFulfillmentResult(
                    false,
                    "An active or suspended source package must be expired during renewal.");
            }

            if (line.Quantity < 1)
            {
                return new LicenseFulfillmentResult(false, "Renewal quantity must be at least 1.");
            }

            if (line.LicenseType is { } renewalType && !Enum.IsDefined(renewalType))
            {
                return new LicenseFulfillmentResult(false, "Renewal license type is invalid.");
            }

            var renewalRequired = line.RenewalRequired ?? source.RenewalRequired;
            var dateError = LicenseManagementLifecycleRules.ValidatePackageDates(
                line.StartDate, line.EndDate, line.IsPerpetual, renewalRequired, line.RenewalDate);
            if (dateError is not null)
            {
                return new LicenseFulfillmentResult(false, dateError);
            }

            if (line.CopySeatAssignments
                && activeRenewalSeatCounts.GetValueOrDefault(line.SourcePackageId) > line.Quantity)
            {
                return new LicenseFulfillmentResult(
                    false,
                    "Renewal quantity cannot be lower than the number of active seats being copied.");
            }

            if (line.CopySeatAssignments
                && (source.LicenseType != LicenseType.NamedUser
                    || (line.LicenseType ?? source.LicenseType) != LicenseType.NamedUser))
            {
                return new LicenseFulfillmentResult(
                    false,
                    "Seat assignments can only be carried between named-user license packages.");
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

            var dateError = LicenseManagementLifecycleRules.ValidatePackageDates(
                line.StartDate, line.EndDate, line.IsPerpetual, false, null);
            if (dateError is not null)
            {
                return new LicenseFulfillmentResult(false, dateError);
            }
        }

        var now = DateTime.UtcNow;
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
        var packageByProductAndType = new Dictionary<(Guid ProductId, LicenseType LicenseType), LicensePackage>();
        foreach (var group in productGroups)
        {
            var defaults = defaultsByProductAndType[group.Key];
            var quantity = group.Sum(line => line.FulfillQuantity);

            var package = new LicensePackage
            {
                PurchaseId = purchase.Id,
                ProductId = group.Key.ProductId,
                LicenseType = group.Key.LicenseType,
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
            packageByProductAndType[group.Key] = package;
        }

        await context.SaveChangesAsync(cancellationToken);

        var createdPackages = new List<LicensePackage>(packageByProductAndType.Values);

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
                RenewalRequired = line.RenewalRequired ?? source.RenewalRequired,
                RenewalDate = line.RenewalDate,
                SerialNumber = source.SerialNumber,
                LicenseKey = source.LicenseKey,
                LicenseKeyIsEncrypted = source.LicenseKeyIsEncrypted,
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
                if (line.ExpireSourcePackage)
                {
                    var seatsToRelease = await context.LicenseSeatAssignments
                        .Where(x => x.PackageId == source.Id
                            && x.Status == LicenseSeatAssignmentStatus.Active)
                        .ToListAsync(cancellationToken);
                    foreach (var seat in seatsToRelease)
                    {
                        seat.Status = LicenseSeatAssignmentStatus.Released;
                        seat.ReleasedDate = DateOnly.FromDateTime(now);
                        seat.UpdatedAt = now;
                        seat.UpdatedBy = request.ActorUserName;
                    }
                }

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
            var package = packageByProductAndType[(item.ProductId, item.LicenseType)];

            if (item.LicenseType == LicenseType.NamedUser)
            {
                var selectedUserIds = line.RequestItemUserIds!.ToHashSet();
                foreach (var user in item.Users.Where(user => selectedUserIds.Contains(user.Id)))
                {
                    var assignment = new LicenseSeatAssignment
                    {
                        PackageId = package.Id,
                        AdObjectId = user.AdObjectId,
                        DisplayName = user.DisplayName
                            ?? user.SamAccountName
                            ?? user.UserPrincipalName
                            ?? user.AdObjectId,
                        SamAccountName = user.SamAccountName,
                        UserPrincipalName = user.UserPrincipalName,
                        Mail = user.Mail,
                        Department = user.Department,
                        Title = user.Title,
                        AssignedDate = DateOnly.FromDateTime(now),
                        Status = LicenseSeatAssignmentStatus.Active,
                        SourceRequestItemId = item.Id,
                        Note = "Created from named-user request fulfillment.",
                        CreatedAt = now,
                        CreatedBy = request.ActorUserName,
                    };
                    await context.LicenseSeatAssignments.AddAsync(assignment, cancellationToken);
                    await WriteAuditAsync(
                        context,
                        "Assign",
                        "LicenseSeatAssignment",
                        assignment.Id,
                        $"License seat assigned to {assignment.DisplayName} from request item {item.Id}.",
                        request.ActorUserId,
                        request.ActorUserName,
                        request.ActorIpAddress,
                        request.ActorUserAgent,
                        cancellationToken);

                    user.Status = LicenseRequestItemUserStatus.Fulfilled;
                    user.UpdatedAt = now;
                    user.UpdatedBy = request.ActorUserName;
                }
            }

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
            + $"{packageByProductAndType.Count} from {request.Lines.Count} request line(s), "
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
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

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
