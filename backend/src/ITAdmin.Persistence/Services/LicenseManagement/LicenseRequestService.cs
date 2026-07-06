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

public sealed class LicenseRequestService(AppDbContext context) : ILicenseRequestService
{
    public async Task<PagedResult<LicenseRequestListItem>> GetListAsync(
        LicenseRequestListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);
        var itemsQuery = context.LicenseRequests
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.RequesterUnitDisplayName, pattern)
                || (x.RequesterManagerName != null && EF.Functions.ILike(x.RequesterManagerName, pattern))
                || (x.ExternalRequestNumber != null && EF.Functions.ILike(x.ExternalRequestNumber, pattern))
                || (x.EbysNumber != null && EF.Functions.ILike(x.EbysNumber, pattern)));
        }

        if (query.Status is { } status)
        {
            itemsQuery = itemsQuery.Where(x => x.Status == status);
        }

        if (query.RequestSource is { } requestSource)
        {
            itemsQuery = itemsQuery.Where(x => x.RequestSource == requestSource);
        }

        if (query.RequestDateFrom is { } requestDateFrom)
        {
            itemsQuery = itemsQuery.Where(x => x.RequestDate >= requestDateFrom);
        }

        if (query.RequestDateTo is { } requestDateTo)
        {
            itemsQuery = itemsQuery.Where(x => x.RequestDate <= requestDateTo);
        }

        if (!string.IsNullOrWhiteSpace(query.RequesterUnitObjectGuid))
        {
            itemsQuery = itemsQuery.Where(x => x.RequesterUnitObjectGuid == query.RequesterUnitObjectGuid);
        }

        if (query.ProductId is { } productId)
        {
            itemsQuery = itemsQuery.Where(x =>
                x.Items.Any(item => item.ProductId == productId));
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await itemsQuery
            .OrderByDescending(x => x.RequestDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LicenseRequestListItem(
                x.Id,
                x.RequestSource,
                x.RequestDate,
                x.ExternalRequestNumber,
                x.EbysNumber,
                x.RequesterUnitDisplayName,
                x.RequesterManagerName,
                x.Items.Count,
                x.Items.SelectMany(i => i.Users).Count(),
                x.EstimatedTotalCost,
                x.Currency,
                x.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<LicenseRequestListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<LicenseRequestDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseRequests
            .AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.Items)
            .ThenInclude(x => x.Users)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);

        return entity is null ? null : MapDetail(entity);
    }

    public async Task<LicenseRequestOperationResult> CreateAsync(
        CreateLicenseRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateRequestPayloadAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return new LicenseRequestOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        var entity = MapRequestEntity(new LicenseRequest(), request, now, request.ActorUserName, isCreate: true);
        await context.LicenseRequests.AddAsync(entity, cancellationToken);

        var userCount = entity.Items.Sum(x => x.Users.Count);
        await WriteAuditAsync(
            context,
            "Create",
            "LicenseRequest",
            entity.Id,
            $"License request created for {entity.RequesterUnitDisplayName} ({entity.Items.Count} products, {userCount} users).",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var detail = await GetByIdAsync(entity.Id, cancellationToken);
        return new LicenseRequestOperationResult(true, "License request created.", detail);
    }

    public async Task<LicenseRequestOperationResult> UpdateAsync(
        UpdateLicenseRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseRequests
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.IsActive, cancellationToken);

        if (entity is null)
        {
            return new LicenseRequestOperationResult(false, "License request was not found.");
        }

        var validationError = await ValidateRequestPayloadAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return new LicenseRequestOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        // Delete the existing items (their users cascade at the database level) with a set-based
        // delete that bypasses the change tracker, then reload the request on a clean tracker
        // before mapping the replacement set. Mutating the originally tracked graph and adding new
        // children in a single SaveChanges makes EF re-point the removed users' foreign key instead
        // of deleting them, producing an UPDATE that affects zero rows (DbUpdateConcurrencyException).
        await context.LicenseRequestItems
            .Where(item => item.RequestId == entity.Id)
            .ExecuteDeleteAsync(cancellationToken);

        context.ChangeTracker.Clear();
        entity = await context.LicenseRequests
            .FirstAsync(x => x.Id == request.Id, cancellationToken);

        MapRequestEntity(entity, ToCreatePayload(request), now, request.ActorUserName, isCreate: false);

        // Force the freshly built child graph to Added. Attaching new items/users to an
        // already-tracked (Unchanged) request lets EF's graph walk mis-classify some children as
        // Modified, emitting an UPDATE against rows that were just deleted (0 rows affected).
        foreach (var item in entity.Items)
        {
            context.Entry(item).State = EntityState.Added;
            foreach (var user in item.Users)
            {
                context.Entry(user).State = EntityState.Added;
            }
        }

        var userCount = entity.Items.Sum(x => x.Users.Count);
        await WriteAuditAsync(
            context,
            "Update",
            "LicenseRequest",
            entity.Id,
            $"License request updated for {entity.RequesterUnitDisplayName} ({entity.Items.Count} products, {userCount} users).",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var detail = await GetByIdAsync(entity.Id, cancellationToken);
        return new LicenseRequestOperationResult(true, "License request updated.", detail);
    }

    private async Task<string?> ValidateRequestPayloadAsync(
        CreateLicenseRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RequestDate == default)
        {
            return "Request date is required.";
        }

        if (request.RequesterUnit is null
            || string.IsNullOrWhiteSpace(request.RequesterUnit.ObjectGuid)
            || string.IsNullOrWhiteSpace(request.RequesterUnit.DisplayName)
            || string.IsNullOrWhiteSpace(request.RequesterUnit.DistinguishedName))
        {
            return "Requester unit is required.";
        }

        if (request.EstimatedTotalCost is < 0)
        {
            return "Estimated total cost cannot be negative.";
        }

        var (externalRequestNumber, ebysNumber, ebysDate) = LicenseRequestRules.NormalizeSourceFields(
            request.RequestSource,
            request.ExternalRequestNumber,
            request.EbysNumber,
            request.EbysDate);

        var sourceValidationError = LicenseRequestRules.ValidateSourceFields(
            request.RequestSource,
            externalRequestNumber,
            ebysNumber,
            ebysDate);

        if (sourceValidationError is not null)
        {
            return sourceValidationError;
        }

        return await ValidateItemsAsync(request.Items, cancellationToken);
    }

    private async Task<string?> ValidateRequestPayloadAsync(
        UpdateLicenseRequestRequest request,
        CancellationToken cancellationToken) =>
        await ValidateRequestPayloadAsync(ToCreatePayload(request), cancellationToken);

    private static CreateLicenseRequestRequest ToCreatePayload(UpdateLicenseRequestRequest request) =>
        new(
            request.RequestSource,
            request.RequestDate,
            request.ExternalRequestNumber,
            request.EbysNumber,
            request.EbysDate,
            request.RequesterUnit,
            request.RequesterManagerName,
            request.Description,
            request.EstimatedTotalCost,
            request.Currency,
            request.VatIncluded,
            request.CostNote,
            request.Items,
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent);

    private async Task<string?> ValidateItemsAsync(
        IReadOnlyList<LicenseRequestItemInput> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return "At least one product item is required.";
        }

        var productIds = new HashSet<Guid>();
        foreach (var item in items)
        {
            if (item.ProductId == Guid.Empty)
            {
                return "Product is required for each item.";
            }

            if (!productIds.Add(item.ProductId))
            {
                return LicenseRequestRules.DuplicateProductMessage;
            }

            if (item.Users.Count == 0)
            {
                return "Each product item must include at least one user.";
            }

            if (!LicenseRequestRules.ManualItemStatuses.Contains(item.Status))
            {
                return "Selected item status cannot be set manually.";
            }

            if (item.EstimatedUnitCost is < 0)
            {
                return "Estimated unit cost cannot be negative.";
            }

            var userIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var user in item.Users)
            {
                if (string.IsNullOrWhiteSpace(user.AdObjectId))
                {
                    return "Each user must have an AD object id.";
                }

                if (!userIds.Add(user.AdObjectId.Trim()))
                {
                    return LicenseRequestRules.DuplicateUserMessage;
                }

                if (!LicenseRequestRules.ManualUserStatuses.Contains(user.Status))
                {
                    return "Selected user status cannot be set manually.";
                }

                if (!LicenseManagementValidation.IsValidEmail(user.Mail))
                {
                    return "One or more user email values are invalid.";
                }
            }

            var product = await context.LicensedProducts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == item.ProductId, cancellationToken);

            if (product is null)
            {
                return "One or more selected products were not found.";
            }

            if (!product.IsActive)
            {
                return "Passive products cannot be added to a request.";
            }
        }

        return null;
    }

    private static LicenseRequest MapRequestEntity(
        LicenseRequest entity,
        CreateLicenseRequestRequest request,
        DateTime now,
        string? actorUserName,
        bool isCreate)
    {
        var (externalRequestNumber, ebysNumber, ebysDate) = LicenseRequestRules.NormalizeSourceFields(
            request.RequestSource,
            request.ExternalRequestNumber,
            request.EbysNumber,
            request.EbysDate);

        entity.RequestSource = request.RequestSource;
        entity.RequestDate = request.RequestDate;
        entity.ExternalRequestNumber = externalRequestNumber;
        entity.EbysNumber = ebysNumber;
        entity.EbysDate = ebysDate;
        entity.RequesterUnitDisplayName = request.RequesterUnit.DisplayName.Trim();
        entity.RequesterUnitDistinguishedName = request.RequesterUnit.DistinguishedName.Trim();
        entity.RequesterUnitObjectGuid = request.RequesterUnit.ObjectGuid.Trim();
        entity.RequesterManagerName = LicenseManagementValidation.TrimOrNull(request.RequesterManagerName);
        entity.Description = LicenseManagementValidation.TrimOrNull(request.Description);
        entity.Currency = LicenseManagementValidation.TrimOrNull(request.Currency);
        entity.VatIncluded = request.VatIncluded;
        entity.CostNote = LicenseManagementValidation.TrimOrNull(request.CostNote);
        entity.IsActive = true;

        entity.Items = request.Items.Select(itemInput =>
        {
            var userCount = itemInput.Users.Count;
            decimal? itemTotal = itemInput.EstimatedUnitCost.HasValue
                ? itemInput.EstimatedUnitCost.Value * userCount
                : null;

            var item = new LicenseRequestItem
            {
                ProductId = itemInput.ProductId,
                RequestedQuantity = userCount,
                ApprovedQuantity = userCount,
                FulfilledQuantity = 0,
                EstimatedUnitCost = itemInput.EstimatedUnitCost,
                EstimatedTotalCost = itemTotal,
                Currency = LicenseManagementValidation.TrimOrNull(itemInput.Currency),
                VatIncluded = itemInput.VatIncluded,
                Justification = LicenseManagementValidation.TrimOrNull(itemInput.Justification),
                Status = itemInput.Status,
                CreatedAt = now,
                CreatedBy = actorUserName,
                Users = itemInput.Users.Select(userInput => new LicenseRequestItemUser
                {
                    AdObjectId = userInput.AdObjectId.Trim(),
                    SamAccountName = LicenseManagementValidation.TrimOrNull(userInput.SamAccountName),
                    UserPrincipalName = LicenseManagementValidation.TrimOrNull(userInput.UserPrincipalName),
                    DisplayName = LicenseManagementValidation.TrimOrNull(userInput.DisplayName),
                    Department = LicenseManagementValidation.TrimOrNull(userInput.Department),
                    Title = LicenseManagementValidation.TrimOrNull(userInput.Title),
                    Mail = LicenseManagementValidation.TrimOrNull(userInput.Mail),
                    Phone = LicenseManagementValidation.TrimOrNull(userInput.Phone),
                    Status = userInput.Status,
                    CreatedAt = now,
                    CreatedBy = actorUserName,
                }).ToList(),
            };

            return item;
        }).ToList();

        // Request status is always derived from item statuses; it is never set manually.
        entity.Status = LicenseRequestRules.DeriveRequestStatus(entity.Items.Select(x => x.Status));

        entity.EstimatedTotalCost = request.EstimatedTotalCost
            ?? (entity.Items.All(x => x.EstimatedTotalCost.HasValue)
                ? entity.Items.Sum(x => x.EstimatedTotalCost)
                : null);

        if (isCreate)
        {
            entity.CreatedAt = now;
            entity.CreatedBy = actorUserName;
        }
        else
        {
            entity.UpdatedAt = now;
            entity.UpdatedBy = actorUserName;
        }

        return entity;
    }

    private static LicenseRequestDetail MapDetail(LicenseRequest entity) =>
        new(
            entity.Id,
            entity.RequestSource,
            entity.RequestDate,
            entity.ExternalRequestNumber,
            entity.EbysNumber,
            entity.EbysDate,
            entity.RequesterUnitDisplayName,
            entity.RequesterUnitDistinguishedName,
            entity.RequesterUnitObjectGuid,
            entity.RequesterManagerName,
            entity.Description,
            entity.Status,
            entity.EstimatedTotalCost,
            entity.Currency,
            entity.VatIncluded,
            entity.CostNote,
            entity.IsActive,
            entity.Items
                .OrderBy(x => x.Product.Name)
                .Select(item => new LicenseRequestItemDetail(
                    item.Id,
                    item.ProductId,
                    item.Product.Name,
                    item.RequestedQuantity,
                    item.ApprovedQuantity,
                    item.FulfilledQuantity,
                    item.EstimatedUnitCost,
                    item.EstimatedTotalCost,
                    item.Currency,
                    item.VatIncluded,
                    item.Justification,
                    item.Status,
                    item.Users
                        .OrderBy(u => u.DisplayName ?? u.SamAccountName ?? u.AdObjectId)
                        .Select(user => new LicenseRequestItemUserDetail(
                            user.Id,
                            user.AdObjectId,
                            user.SamAccountName,
                            user.UserPrincipalName,
                            user.DisplayName,
                            user.Department,
                            user.Title,
                            user.Mail,
                            user.Phone,
                            user.Status))
                        .ToList()))
                .ToList(),
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);
}
