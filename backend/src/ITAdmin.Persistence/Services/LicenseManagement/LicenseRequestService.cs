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
            .ThenBy(x => x.Id)
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
                x.Items.Sum(i => i.RequestedQuantity),
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
            .Include(x => x.Items)
            .ThenInclude(x => x.Users)
            .Include(x => x.Items)
            .ThenInclude(x => x.Fulfillments)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.IsActive, cancellationToken);

        if (entity is null)
        {
            return new LicenseRequestOperationResult(false, "License request was not found.");
        }

        var validationError = await ValidateRequestPayloadAsync(request, entity.Items, cancellationToken);
        if (validationError is not null)
        {
            return new LicenseRequestOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        foreach (var existingItem in entity.Items.ToList())
        {
            if (request.Items.Any(x => x.ProductId == existingItem.ProductId))
            {
                continue;
            }

            if (existingItem.Fulfillments.Count > 0)
            {
                return new LicenseRequestOperationResult(
                    false,
                    "A request item with fulfillment history cannot be removed.");
            }

            entity.Items.Remove(existingItem);
            context.LicenseRequestItems.Remove(existingItem);
        }

        ApplyRequestFields(entity, ToCreatePayload(request), now, request.ActorUserName, isCreate: false);

        foreach (var itemInput in request.Items)
        {
            var item = entity.Items.FirstOrDefault(x => x.ProductId == itemInput.ProductId);
            if (item is null)
            {
                item = CreateItem(itemInput, now, request.ActorUserName);
                entity.Items.Add(item);
                continue;
            }

            var hasFulfillmentHistory = item.Fulfillments.Count > 0;
            if (hasFulfillmentHistory && item.LicenseType != itemInput.LicenseType)
            {
                return new LicenseRequestOperationResult(
                    false,
                    "The license type of a request item with fulfillment history cannot be changed.");
            }

            if (hasFulfillmentHistory
                && item.RequestedQuantity != ResolveRequestedQuantity(itemInput))
            {
                return new LicenseRequestOperationResult(
                    false,
                    "The requested quantity of a request item with fulfillment history cannot be changed.");
            }

            var requestedUserIds = itemInput.Users
                .Select(x => x.AdObjectId.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var currentUserIds = item.Users
                .Select(x => x.AdObjectId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (hasFulfillmentHistory && !requestedUserIds.SetEquals(currentUserIds))
            {
                return new LicenseRequestOperationResult(
                    false,
                    "Users on a request item with fulfillment history cannot be added or removed.");
            }

            var removedUsers = item.Users
                .Where(x => !requestedUserIds.Contains(x.AdObjectId))
                .ToList();
            UpdateItem(item, itemInput, now, request.ActorUserName, preserveWorkflowState: hasFulfillmentHistory);
            context.LicenseRequestItemUsers.RemoveRange(removedUsers);
            foreach (var addedUser in item.Users.Where(x => x.CreatedAt == now))
            {
                context.Entry(addedUser).State = EntityState.Added;
            }
        }

        entity.Status = LicenseRequestRules.DeriveRequestStatus(entity.Items.Select(x => x.Status));
        entity.EstimatedTotalCost = request.EstimatedTotalCost
            ?? (entity.Items.All(x => x.EstimatedTotalCost.HasValue)
                ? entity.Items.Sum(x => x.EstimatedTotalCost)
                : null);

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
        ICollection<LicenseRequestItem> existingItems,
        CancellationToken cancellationToken)
    {
        var payload = ToCreatePayload(request);
        if (payload.RequestDate == default)
        {
            return "Request date is required.";
        }

        if (payload.RequesterUnit is null
            || string.IsNullOrWhiteSpace(payload.RequesterUnit.ObjectGuid)
            || string.IsNullOrWhiteSpace(payload.RequesterUnit.DisplayName)
            || string.IsNullOrWhiteSpace(payload.RequesterUnit.DistinguishedName))
        {
            return "Requester unit is required.";
        }

        if (payload.EstimatedTotalCost is < 0)
        {
            return "Estimated total cost cannot be negative.";
        }

        var normalized = LicenseRequestRules.NormalizeSourceFields(
            payload.RequestSource,
            payload.ExternalRequestNumber,
            payload.EbysNumber,
            payload.EbysDate);
        var sourceValidationError = LicenseRequestRules.ValidateSourceFields(
            payload.RequestSource,
            normalized.ExternalRequestNumber,
            normalized.EbysNumber,
            normalized.EbysDate);

        return sourceValidationError
            ?? await ValidateItemsAsync(
                payload.Items,
                cancellationToken,
                existingItems.ToDictionary(x => x.ProductId));
    }

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
        CancellationToken cancellationToken,
        IReadOnlyDictionary<Guid, LicenseRequestItem>? existingItems = null)
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

            if (!Enum.IsDefined(item.LicenseType))
            {
                return "License type is invalid for one or more request items.";
            }

            var requestedQuantity = ResolveRequestedQuantity(item);
            if (item.LicenseType == LicenseType.NamedUser)
            {
                if (item.Users.Count == 0)
                {
                    return "Named-user license items must include at least one user.";
                }

                if (requestedQuantity != item.Users.Count)
                {
                    return "Named-user license quantity must match the selected user count.";
                }
            }
            else
            {
                if (requestedQuantity < 1)
                {
                    return "Requested quantity must be at least 1 for each item.";
                }

                if (item.Users.Count > 0)
                {
                    return "Quantity-based license items cannot include named users.";
                }
            }

            LicenseRequestItem? existingItem = null;
            existingItems?.TryGetValue(item.ProductId, out existingItem);
            var hasFulfillmentHistory = existingItem?.Fulfillments.Count > 0;
            if (hasFulfillmentHistory && item.Status != existingItem!.Status)
            {
                return "The workflow status of a fulfilled request item cannot be changed from the edit form.";
            }

            if (!hasFulfillmentHistory && !LicenseRequestRules.ManualItemStatuses.Contains(item.Status))
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

                var existingUser = existingItem?.Users.FirstOrDefault(x =>
                    string.Equals(x.AdObjectId, user.AdObjectId.Trim(), StringComparison.OrdinalIgnoreCase));
                if (hasFulfillmentHistory && existingUser is not null && user.Status != existingUser.Status)
                {
                    return "The workflow status of a user on a fulfilled request item cannot be changed from the edit form.";
                }

                if (!hasFulfillmentHistory && !LicenseRequestRules.ManualUserStatuses.Contains(user.Status))
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
        ApplyRequestFields(entity, request, now, actorUserName, isCreate);
        entity.Items = request.Items.Select(itemInput => CreateItem(itemInput, now, actorUserName)).ToList();

        // Request status is always derived from item statuses; it is never set manually.
        entity.Status = LicenseRequestRules.DeriveRequestStatus(entity.Items.Select(x => x.Status));
        entity.EstimatedTotalCost = request.EstimatedTotalCost
            ?? (entity.Items.All(x => x.EstimatedTotalCost.HasValue)
                ? entity.Items.Sum(x => x.EstimatedTotalCost)
                : null);

        return entity;
    }

    private static void ApplyRequestFields(
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
    }

    private static LicenseRequestItem CreateItem(
        LicenseRequestItemInput input,
        DateTime now,
        string? actorUserName)
    {
        var item = new LicenseRequestItem
        {
            ProductId = input.ProductId,
            LicenseType = input.LicenseType,
            CreatedAt = now,
            CreatedBy = actorUserName,
        };
        UpdateItem(item, input, now, actorUserName, preserveWorkflowState: false);
        return item;
    }

    private static void UpdateItem(
        LicenseRequestItem item,
        LicenseRequestItemInput input,
        DateTime now,
        string? actorUserName,
        bool preserveWorkflowState)
    {
        var requestedQuantity = ResolveRequestedQuantity(input);
        item.LicenseType = input.LicenseType;
        item.RequestedQuantity = requestedQuantity;
        item.EstimatedUnitCost = input.EstimatedUnitCost;
        item.EstimatedTotalCost = input.EstimatedUnitCost.HasValue
            ? input.EstimatedUnitCost.Value * requestedQuantity
            : null;
        item.Currency = LicenseManagementValidation.TrimOrNull(input.Currency);
        item.VatIncluded = input.VatIncluded;
        item.Justification = LicenseManagementValidation.TrimOrNull(input.Justification);

        if (!preserveWorkflowState)
        {
            item.ApprovedQuantity = requestedQuantity;
            item.FulfilledQuantity = 0;
            item.Status = input.Status;
        }

        foreach (var existingUser in item.Users.ToList())
        {
            if (!input.Users.Any(x => string.Equals(
                    x.AdObjectId.Trim(),
                    existingUser.AdObjectId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                item.Users.Remove(existingUser);
            }
        }

        foreach (var userInput in input.Users)
        {
            var user = item.Users.FirstOrDefault(x => string.Equals(
                x.AdObjectId,
                userInput.AdObjectId.Trim(),
                StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                user = new LicenseRequestItemUser
                {
                    AdObjectId = userInput.AdObjectId.Trim(),
                    CreatedAt = now,
                    CreatedBy = actorUserName,
                };
                item.Users.Add(user);
            }

            user.SamAccountName = LicenseManagementValidation.TrimOrNull(userInput.SamAccountName);
            user.UserPrincipalName = LicenseManagementValidation.TrimOrNull(userInput.UserPrincipalName);
            user.DisplayName = LicenseManagementValidation.TrimOrNull(userInput.DisplayName);
            user.Department = LicenseManagementValidation.TrimOrNull(userInput.Department);
            user.Title = LicenseManagementValidation.TrimOrNull(userInput.Title);
            user.Mail = LicenseManagementValidation.TrimOrNull(userInput.Mail);
            user.Phone = LicenseManagementValidation.TrimOrNull(userInput.Phone);
            if (!preserveWorkflowState)
            {
                user.Status = userInput.Status;
            }
            if (user.CreatedAt != now)
            {
                user.UpdatedAt = now;
                user.UpdatedBy = actorUserName;
            }
        }

        if (item.CreatedAt != now)
        {
            item.UpdatedAt = now;
            item.UpdatedBy = actorUserName;
        }
    }

    private static int ResolveRequestedQuantity(LicenseRequestItemInput input) =>
        input.RequestedQuantity
        ?? (input.LicenseType == LicenseType.NamedUser ? input.Users.Count : 0);

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
                    item.LicenseType,
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
