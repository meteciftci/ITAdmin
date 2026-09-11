using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services.LicenseManagement;
using ITAdmin.UnitTests.TestInfrastructure;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicenseRequestFulfillmentServiceTests
{
    [Fact]
    public async Task GetCandidates_IncludesOpenLinesForTriageAndFulfillment()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context, "Photoshop");
        await SeedRequestItemAsync(context, product, requested: 5, approved: 5, status: LicenseRequestItemStatus.Approved);
        await SeedRequestItemAsync(context, product, requested: 3, approved: null, status: LicenseRequestItemStatus.Pending);
        await SeedRequestItemAsync(context, product, requested: 2, approved: 2, fulfilled: 2, status: LicenseRequestItemStatus.Fulfilled);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.GetCandidatesAsync(
            new LicenseFulfillmentCandidateQuery(null, null, null, 1, 20), CancellationToken.None);

        // Approved (fulfillable) + Pending (triage only) are returned; the Fulfilled item is excluded.
        Assert.Equal(2, result.TotalCount);
        var approved = result.Items.Single(x => x.ItemStatus == LicenseRequestItemStatus.Approved);
        Assert.Equal(5, approved.RemainingQuantity);
        Assert.True(approved.IsFulfillable);
        var pending = result.Items.Single(x => x.ItemStatus == LicenseRequestItemStatus.Pending);
        Assert.False(pending.IsFulfillable);
    }

    [Fact]
    public async Task Triage_DerivesRequestStatusFromItems()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 4, approved: null, status: LicenseRequestItemStatus.Pending);
        var service = new LicenseRequestFulfillmentService(context);

        await service.TriageAsync(
            new TriageLicenseRequestItemsRequest(
                [new TriageLicenseRequestItemInput(item, LicenseRequestItemStatus.Approved, 4)],
                null, "tester", null, null),
            CancellationToken.None);

        var request = await context.LicenseRequests.AsNoTracking()
            .FirstAsync(x => x.Items.Any(i => i.Id == item));
        Assert.Equal(LicenseRequestStatus.InReview, request.Status);
    }

    [Fact]
    public async Task Triage_ApprovesItemAndSetsApprovedQuantity()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 10, approved: null, status: LicenseRequestItemStatus.Pending);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.TriageAsync(
            new TriageLicenseRequestItemsRequest(
                [new TriageLicenseRequestItemInput(item, LicenseRequestItemStatus.Approved, 7)],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updated = await context.LicenseRequestItems.AsNoTracking().FirstAsync(x => x.Id == item);
        Assert.Equal(LicenseRequestItemStatus.Approved, updated.Status);
        Assert.Equal(7, updated.ApprovedQuantity);
    }

    [Fact]
    public async Task Triage_ApprovedQuantityAboveRequested_Fails()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 5, approved: null, status: LicenseRequestItemStatus.Pending);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.TriageAsync(
            new TriageLicenseRequestItemsRequest(
                [new TriageLicenseRequestItemInput(item, LicenseRequestItemStatus.Approved, 6)],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("quantity", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Triage_NamedUser_ApprovesOnlyTheExplicitlySelectedUsers()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var (item, users) = await SeedNamedUserRequestItemAsync(
            context, product, requested: 3, approved: null, LicenseRequestItemStatus.Pending);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.TriageAsync(
            new TriageLicenseRequestItemsRequest(
                [new TriageLicenseRequestItemInput(
                    item, LicenseRequestItemStatus.Approved, 2, [users[0], users[2]])],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var statuses = await context.LicenseRequestItemUsers.AsNoTracking()
            .Where(x => x.RequestItemId == item)
            .ToDictionaryAsync(x => x.Id, x => x.Status);
        Assert.Equal(LicenseRequestItemUserStatus.Approved, statuses[users[0]]);
        Assert.Equal(LicenseRequestItemUserStatus.Rejected, statuses[users[1]]);
        Assert.Equal(LicenseRequestItemUserStatus.Approved, statuses[users[2]]);
    }

    [Fact]
    public async Task Convert_NewPurchase_CreatesDraftPurchasePackageAndFulfillmentLink()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 5, approved: 5, status: LicenseRequestItemStatus.Approved);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(
            BuildConvert(product, [(item, 5)]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PurchaseId);
        var purchase = await context.LicensePurchases.AsNoTracking().SingleAsync();
        Assert.Equal(LicensePurchaseStatus.Draft, purchase.Status);
        var package = await context.LicensePackages.AsNoTracking().SingleAsync();
        Assert.Equal(5, package.Quantity);
        Assert.Equal(purchase.Id, package.PurchaseId);
        var link = await context.LicenseRequestItemFulfillments.AsNoTracking().SingleAsync();
        Assert.Equal(item, link.RequestItemId);
        Assert.Equal(package.Id, link.PackageId);
        Assert.Equal(5, link.Quantity);
    }

    [Fact]
    public async Task Convert_NamedUserPartialFulfillment_CreatesSeatsAndTracksExactUsers()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var (item, users) = await SeedNamedUserRequestItemAsync(
            context, product, requested: 2, approved: 2, LicenseRequestItemStatus.Approved);
        var service = new LicenseRequestFulfillmentService(context);

        var first = await service.ConvertToPurchaseAsync(
            BuildNamedUserConvert(product, item, [users[1]]), CancellationToken.None);

        Assert.True(first.IsSuccess);
        var afterFirst = await context.LicenseRequestItems.AsNoTracking().SingleAsync(x => x.Id == item);
        Assert.Equal(1, afterFirst.FulfilledQuantity);
        Assert.Equal(LicenseRequestItemStatus.PartiallyFulfilled, afterFirst.Status);
        var userStatuses = await context.LicenseRequestItemUsers.AsNoTracking()
            .Where(x => x.RequestItemId == item)
            .ToDictionaryAsync(x => x.Id, x => x.Status);
        Assert.Equal(LicenseRequestItemUserStatus.Approved, userStatuses[users[0]]);
        Assert.Equal(LicenseRequestItemUserStatus.Fulfilled, userStatuses[users[1]]);
        var firstSeat = await context.LicenseSeatAssignments.AsNoTracking().SingleAsync();
        Assert.Equal("user-2", firstSeat.AdObjectId);
        Assert.Equal(item, firstSeat.SourceRequestItemId);

        var repeated = await service.ConvertToPurchaseAsync(
            BuildNamedUserConvert(product, item, [users[1]]), CancellationToken.None);
        Assert.False(repeated.IsSuccess);
        Assert.Single(context.LicensePurchases);
        Assert.Single(context.LicensePackages);
        Assert.Single(context.LicenseSeatAssignments);

        var second = await service.ConvertToPurchaseAsync(
            BuildNamedUserConvert(product, item, [users[0]]), CancellationToken.None);

        Assert.True(second.IsSuccess);
        var afterSecond = await context.LicenseRequestItems.AsNoTracking().SingleAsync(x => x.Id == item);
        Assert.Equal(LicenseRequestItemStatus.Fulfilled, afterSecond.Status);
        Assert.Equal(2, await context.LicenseSeatAssignments.CountAsync());
        Assert.All(
            await context.LicenseRequestItemUsers.AsNoTracking().Where(x => x.RequestItemId == item).ToListAsync(),
            user => Assert.Equal(LicenseRequestItemUserStatus.Fulfilled, user.Status));
    }

    [Fact]
    public async Task Convert_NamedUserWithoutExactApprovedUsers_FailsWithoutWrites()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var (item, users) = await SeedNamedUserRequestItemAsync(
            context, product, requested: 2, approved: 2, LicenseRequestItemStatus.Approved);
        var service = new LicenseRequestFulfillmentService(context);

        var missingUsers = await service.ConvertToPurchaseAsync(
            BuildNamedUserConvert(product, item, []), CancellationToken.None);
        var foreignUser = await service.ConvertToPurchaseAsync(
            BuildNamedUserConvert(product, item, [Guid.NewGuid()]), CancellationToken.None);

        Assert.False(missingUsers.IsSuccess);
        Assert.False(foreignUser.IsSuccess);
        Assert.Empty(context.LicensePurchases);
        Assert.Empty(context.LicensePackages);
        Assert.Empty(context.LicenseSeatAssignments);
        Assert.All(
            await context.LicenseRequestItemUsers.AsNoTracking().Where(x => users.Contains(x.Id)).ToListAsync(),
            user => Assert.Equal(LicenseRequestItemUserStatus.Approved, user.Status));
    }

    [Fact]
    public async Task Convert_SameNamedUserAcrossRequestsForOnePackage_IsRejected()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var (firstItem, firstUsers) = await SeedNamedUserRequestItemAsync(
            context, product, requested: 1, approved: 1, LicenseRequestItemStatus.Approved);
        var (secondItem, secondUsers) = await SeedNamedUserRequestItemAsync(
            context, product, requested: 1, approved: 1, LicenseRequestItemStatus.Approved);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(
            new ConvertLicenseRequestItemsRequest(
                null,
                new ConvertFulfillmentNewPurchaseInput(
                    LicensePurchaseType.DirectPurchase, "Duplicate holder", null,
                    new DateOnly(2026, 7, 1), null, null, null, "TRY", false, null),
                [
                    new ConvertFulfillmentLineInput(firstItem, 1, [firstUsers.Single()]),
                    new ConvertFulfillmentLineInput(secondItem, 1, [secondUsers.Single()]),
                ],
                [new ConvertFulfillmentPackageDefaultsInput(
                    product, LicenseType.NamedUser, null, null, false)],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(context.LicensePurchases);
        Assert.Empty(context.LicensePackages);
        Assert.Empty(context.LicenseSeatAssignments);
    }

    [Fact]
    public async Task Convert_AggregatesSameProductAcrossRequestsIntoOnePackage()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item1 = await SeedRequestItemAsync(context, product, requested: 3, approved: 3, status: LicenseRequestItemStatus.Approved);
        var item2 = await SeedRequestItemAsync(context, product, requested: 4, approved: 4, status: LicenseRequestItemStatus.Approved);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(
            BuildConvert(product, [(item1, 3), (item2, 4)]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var package = await context.LicensePackages.AsNoTracking().SingleAsync();
        Assert.Equal(7, package.Quantity);
        Assert.Equal(2, await context.LicenseRequestItemFulfillments.CountAsync());
    }

    [Fact]
    public async Task Convert_SameProductWithDifferentLicenseTypes_CreatesSeparatePackages()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var concurrentItem = await SeedRequestItemAsync(
            context, product, requested: 5, approved: 5,
            status: LicenseRequestItemStatus.Approved,
            licenseType: LicenseType.Concurrent);
        var serverItem = await SeedRequestItemAsync(
            context, product, requested: 2, approved: 2,
            status: LicenseRequestItemStatus.Approved,
            licenseType: LicenseType.ServerBased);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(
            new ConvertLicenseRequestItemsRequest(
                null,
                new ConvertFulfillmentNewPurchaseInput(
                    LicensePurchaseType.DirectPurchase, "Mixed allocation", null, null,
                    null, null, null, "TRY", false, null),
                [
                    new ConvertFulfillmentLineInput(concurrentItem, 5),
                    new ConvertFulfillmentLineInput(serverItem, 2),
                ],
                [
                    new ConvertFulfillmentPackageDefaultsInput(product, LicenseType.Concurrent, null, null, false),
                    new ConvertFulfillmentPackageDefaultsInput(product, LicenseType.ServerBased, null, null, false),
                ],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var packages = await context.LicensePackages.AsNoTracking().OrderBy(x => x.LicenseType).ToListAsync();
        Assert.Equal(2, packages.Count);
        Assert.Contains(packages, x => x.LicenseType == LicenseType.Concurrent && x.Quantity == 5);
        Assert.Contains(packages, x => x.LicenseType == LicenseType.ServerBased && x.Quantity == 2);
    }

    [Fact]
    public async Task Convert_PartialFulfillment_SetsPartiallyFulfilledThenFulfilled()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 10, approved: 10, status: LicenseRequestItemStatus.Approved);
        var service = new LicenseRequestFulfillmentService(context);

        var first = await service.ConvertToPurchaseAsync(BuildConvert(product, [(item, 6)]), CancellationToken.None);
        Assert.True(first.IsSuccess);
        var afterFirst = await context.LicenseRequestItems.AsNoTracking().FirstAsync(x => x.Id == item);
        Assert.Equal(6, afterFirst.FulfilledQuantity);
        Assert.Equal(LicenseRequestItemStatus.PartiallyFulfilled, afterFirst.Status);

        var second = await service.ConvertToPurchaseAsync(BuildConvert(product, [(item, 4)]), CancellationToken.None);
        Assert.True(second.IsSuccess);
        var afterSecond = await context.LicenseRequestItems.AsNoTracking().FirstAsync(x => x.Id == item);
        Assert.Equal(10, afterSecond.FulfilledQuantity);
        Assert.Equal(LicenseRequestItemStatus.Fulfilled, afterSecond.Status);
    }

    [Fact]
    public async Task Convert_FulfillMoreThanRemaining_Fails()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 5, approved: 5, status: LicenseRequestItemStatus.Approved);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(BuildConvert(product, [(item, 6)]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(await context.LicensePurchases.ToListAsync());
    }

    [Fact]
    public async Task Convert_PendingItem_Fails()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 5, approved: null, status: LicenseRequestItemStatus.Pending);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(BuildConvert(product, [(item, 1)]), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("approved", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Convert_ExistingPurchase_AttachesPackage()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 2, approved: 2, status: LicenseRequestItemStatus.Approved);
        var purchase = new LicensePurchase
        {
            PurchaseType = LicensePurchaseType.DirectPurchase,
            Title = "Existing",
            Status = LicensePurchaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicensePurchases.Add(purchase);
        await context.SaveChangesAsync();
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(
            new ConvertLicenseRequestItemsRequest(
                purchase.Id,
                null,
                [new ConvertFulfillmentLineInput(item, 2)],
                [new ConvertFulfillmentPackageDefaultsInput(product, LicenseType.Subscription, null, null, false)],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(purchase.Id, result.PurchaseId);
        Assert.Equal(1, await context.LicensePurchases.CountAsync());
        var package = await context.LicensePackages.AsNoTracking().SingleAsync();
        Assert.Equal(purchase.Id, package.PurchaseId);
    }

    [Fact]
    public async Task Convert_WritesFulfillAndPurchaseAudit()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var item = await SeedRequestItemAsync(context, product, requested: 2, approved: 2, status: LicenseRequestItemStatus.Approved);
        var service = new LicenseRequestFulfillmentService(context);

        await service.ConvertToPurchaseAsync(BuildConvert(product, [(item, 2)]), CancellationToken.None);

        var actions = await context.AuditLogs.AsNoTracking().Select(x => x.Action).ToListAsync();
        Assert.Contains("Fulfill", actions);
        Assert.Contains("Create", actions);
    }

    [Fact]
    public async Task Convert_RenewalLine_CopiesPackageExpiresSourceAndCarriesSeats()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);

        var sourcePurchase = new LicensePurchase
        {
            PurchaseType = LicensePurchaseType.DirectPurchase,
            Title = "Original purchase",
            Status = LicensePurchaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicensePurchases.Add(sourcePurchase);
        await context.SaveChangesAsync();

        var source = new LicensePackage
        {
            PurchaseId = sourcePurchase.Id,
            ProductId = product,
            LicenseType = LicenseType.NamedUser,
            Quantity = 5,
            SerialNumber = "SER-1",
            IsActive = true,
            Status = LicensePackageStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicensePackages.Add(source);
        await context.SaveChangesAsync();

        var seat = new LicenseSeatAssignment
        {
            PackageId = source.Id,
            DisplayName = "Ada Lovelace",
            Mail = "ada@example.com",
            AssignedDate = new DateOnly(2026, 1, 1),
            Status = LicenseSeatAssignmentStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicenseSeatAssignments.Add(seat);
        await context.SaveChangesAsync();

        var service = new LicenseRequestFulfillmentService(context);
        var result = await service.ConvertToPurchaseAsync(
            new ConvertLicenseRequestItemsRequest(
                null,
                new ConvertFulfillmentNewPurchaseInput(
                    LicensePurchaseType.Renewal, "Renewal 2027", null, new DateOnly(2027, 1, 1),
                    null, null, null, "TRY", false, null),
                [],
                [],
                null, "tester", null, null,
                RenewalLines:
                [
                    new ConvertFulfillmentRenewalLineInput(
                        source.Id, 5, null, new DateOnly(2027, 1, 1), new DateOnly(2028, 1, 1),
                        false, ExpireSourcePackage: true, CopySeatAssignments: true),
                ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var refreshedSource = await context.LicensePackages.AsNoTracking().FirstAsync(x => x.Id == source.Id);
        Assert.Equal(LicensePackageStatus.Expired, refreshedSource.Status);
        Assert.False(refreshedSource.IsActive);

        var renewal = await context.LicensePackages.AsNoTracking()
            .FirstAsync(x => x.PreviousPackageId == source.Id);
        Assert.Equal("SER-1", renewal.SerialNumber);
        Assert.Equal(5, renewal.Quantity);

        var sourceSeat = await context.LicenseSeatAssignments.AsNoTracking().FirstAsync(x => x.Id == seat.Id);
        Assert.Equal(LicenseSeatAssignmentStatus.Transferred, sourceSeat.Status);

        var carried = await context.LicenseSeatAssignments.AsNoTracking()
            .FirstAsync(x => x.PackageId == renewal.Id && x.Status == LicenseSeatAssignmentStatus.Active);
        Assert.Equal(seat.Id, carried.ReplacesAssignmentId);
        Assert.Equal("Ada Lovelace", carried.DisplayName);
    }

    [Fact]
    public async Task Convert_ManualLine_AddsPackageWithoutRequestOrRenewalLink()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(
            new ConvertLicenseRequestItemsRequest(
                null,
                new ConvertFulfillmentNewPurchaseInput(
                    LicensePurchaseType.DirectPurchase, "Manual purchase", null, null,
                    null, null, null, "TRY", false, null),
                [],
                [],
                null, "tester", null, null,
                ManualLines:
                [
                    new ConvertFulfillmentManualLineInput(product, 3, LicenseType.Perpetual, null, null, true),
                ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var package = await context.LicensePackages.AsNoTracking().SingleAsync();
        Assert.Equal(3, package.Quantity);
        Assert.Equal(LicenseType.Perpetual, package.LicenseType);
        Assert.Null(package.PreviousPackageId);
    }

    private static ConvertLicenseRequestItemsRequest BuildConvert(
        Guid productId,
        (Guid ItemId, int Quantity)[] lines) =>
        new(
            null,
            new ConvertFulfillmentNewPurchaseInput(
                LicensePurchaseType.DirectPurchase, "Fulfillment purchase", null, new DateOnly(2026, 7, 1),
                null, null, null, "TRY", false, null),
            lines.Select(x => new ConvertFulfillmentLineInput(x.ItemId, x.Quantity)).ToList(),
            [new ConvertFulfillmentPackageDefaultsInput(productId, LicenseType.Subscription, null, null, false)],
            null, "tester", null, null);

    private static ConvertLicenseRequestItemsRequest BuildNamedUserConvert(
        Guid productId,
        Guid itemId,
        IReadOnlyList<Guid> userIds) =>
        new(
            null,
            new ConvertFulfillmentNewPurchaseInput(
                LicensePurchaseType.DirectPurchase, "Named-user fulfillment", null,
                new DateOnly(2026, 7, 1), null, null, null, "TRY", false, null),
            [new ConvertFulfillmentLineInput(itemId, userIds.Count, userIds)],
            [new ConvertFulfillmentPackageDefaultsInput(productId, LicenseType.NamedUser, null, null, false)],
            null, "tester", null, null);

    private static async Task<(Guid ItemId, List<Guid> UserIds)> SeedNamedUserRequestItemAsync(
        AppDbContext context,
        Guid productId,
        int requested,
        int? approved,
        LicenseRequestItemStatus status)
    {
        var itemId = await SeedRequestItemAsync(
            context, productId, requested, approved, status, licenseType: LicenseType.NamedUser);
        var users = Enumerable.Range(1, requested)
            .Select(index => new LicenseRequestItemUser
            {
                RequestItemId = itemId,
                AdObjectId = $"user-{index}",
                DisplayName = $"User {index}",
                UserPrincipalName = $"user-{index}@test.local",
                Mail = $"user-{index}@test.local",
                Status = approved.HasValue
                    ? LicenseRequestItemUserStatus.Approved
                    : LicenseRequestItemUserStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddSeconds(index),
                CreatedBy = "seed",
            })
            .ToList();
        context.LicenseRequestItemUsers.AddRange(users);
        await context.SaveChangesAsync();
        return (itemId, users.Select(x => x.Id).ToList());
    }

    private static async Task<Guid> SeedProductAsync(AppDbContext context, string name = "Photoshop", bool isActive = true)
    {
        var category = new LicenseProductCategory
        {
            Name = "Grafik",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicenseProductCategories.Add(category);
        await context.SaveChangesAsync();

        var product = new LicensedProduct
        {
            Name = name,
            Brand = "Adobe",
            CategoryId = category.Id,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicensedProducts.Add(product);
        await context.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<Guid> SeedRequestItemAsync(
        AppDbContext context,
        Guid productId,
        int requested,
        int? approved,
        LicenseRequestItemStatus status,
        int fulfilled = 0,
        LicenseType licenseType = LicenseType.Subscription)
    {
        var request = new LicenseRequest
        {
            RequestSource = LicenseRequestSource.Email,
            RequestDate = new DateOnly(2026, 6, 29),
            RequesterUnitDisplayName = "Bilgi İşlem",
            RequesterUnitDistinguishedName = "OU=BI,DC=test",
            RequesterUnitObjectGuid = "ou-guid",
            Status = LicenseRequestStatus.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
            Items =
            [
                new LicenseRequestItem
                {
                    ProductId = productId,
                    LicenseType = licenseType,
                    RequestedQuantity = requested,
                    ApprovedQuantity = approved,
                    FulfilledQuantity = fulfilled,
                    Status = status,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "seed",
                },
            ],
        };
        context.LicenseRequests.Add(request);
        await context.SaveChangesAsync();
        return request.Items.First().Id;
    }

    private static AppDbContext CreateDbContext()
    {
        var (_, context) = SqliteTestDbContextFactory.CreateAsync().GetAwaiter().GetResult();
        return context;
    }
}
