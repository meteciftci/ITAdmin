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
    public async Task GetCandidates_ReturnsOnlyApprovedNotFullyFulfilledItems()
    {
        await using var context = CreateDbContext();
        var product = await SeedProductAsync(context, "Photoshop");
        await SeedRequestItemAsync(context, product, requested: 5, approved: 5, status: LicenseRequestItemStatus.Approved);
        await SeedRequestItemAsync(context, product, requested: 3, approved: null, status: LicenseRequestItemStatus.Pending);
        await SeedRequestItemAsync(context, product, requested: 2, approved: 2, fulfilled: 2, status: LicenseRequestItemStatus.Fulfilled);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.GetCandidatesAsync(
            new LicenseFulfillmentCandidateQuery(null, null, null, 1, 20), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(5, result.Items.Single().RemainingQuantity);
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
        int fulfilled = 0)
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
