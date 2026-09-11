using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services.LicenseManagement;
using ITAdmin.UnitTests.TestInfrastructure;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.LicenseManagement;

/// <summary>
/// Executable specifications for confirmed license-management risks. Resolved risks remain active
/// as regression coverage; unresolved risks stay skipped until their implementation phase.
/// </summary>
public sealed class LicenseManagementKnownRiskTests
{
    [Fact]
    public async Task UpdateFulfilledRequest_PreservesItemIdentityAndFulfillmentHistory()
    {
        await using var context = CreateDbContext();
        var (productId, purchaseId, packageId) = await SeedPackageAsync(context, quantity: 2);
        var request = await SeedRequestAsync(context, productId, LicenseRequestItemStatus.Fulfilled, fulfilled: 1);
        var originalItemId = request.Items.Single().Id;

        context.LicenseRequestItemFulfillments.Add(new LicenseRequestItemFulfillment
        {
            RequestItemId = originalItemId,
            PackageId = packageId,
            Quantity = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        });
        await context.SaveChangesAsync();

        var service = new LicenseRequestService(context);
        var result = await service.UpdateAsync(
            new UpdateLicenseRequestRequest(
                request.Id,
                LicenseRequestSource.Email,
                request.RequestDate,
                null,
                null,
                null,
                new LicenseRequestOuSnapshot("ou-guid", "IT", "OU=IT,DC=test"),
                null,
                "Updated without replacing fulfilled history",
                null,
                "TRY",
                false,
                null,
                [new LicenseRequestItemInput(
                    productId,
                    null,
                    "TRY",
                    false,
                    null,
                    LicenseRequestItemStatus.Fulfilled,
                    [BuildUser("user-1", LicenseRequestItemUserStatus.Fulfilled)])],
                null,
                "tester",
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(originalItemId, result.Request!.Items.Single().Id);
        Assert.True(await context.LicenseRequestItemFulfillments.AnyAsync(x => x.RequestItemId == originalItemId));
        Assert.True(await context.LicensePurchases.AnyAsync(x => x.Id == purchaseId));
    }

    [Fact]
    public async Task RenewalWithMoreActiveSeatsThanTargetCapacity_IsRejected()
    {
        await using var context = CreateDbContext();
        var (productId, _, sourcePackageId) = await SeedPackageAsync(context, quantity: 2);
        await SeedSeatAsync(context, sourcePackageId, "Ada", "ada@test.local");
        await SeedSeatAsync(context, sourcePackageId, "Grace", "grace@test.local");

        var service = new LicenseRequestFulfillmentService(context);
        var result = await service.ConvertToPurchaseAsync(
            new ConvertLicenseRequestItemsRequest(
                null,
                new ConvertFulfillmentNewPurchaseInput(
                    LicensePurchaseType.Renewal,
                    "Undersized renewal",
                    null,
                    new DateOnly(2027, 1, 1),
                    null,
                    null,
                    null,
                    "TRY",
                    false,
                    null),
                [],
                [],
                null,
                "tester",
                null,
                null,
                RenewalLines:
                [
                    new ConvertFulfillmentRenewalLineInput(
                        sourcePackageId,
                        1,
                        null,
                        new DateOnly(2027, 1, 1),
                        new DateOnly(2027, 12, 31),
                        false,
                        ExpireSourcePackage: true,
                        CopySeatAssignments: true),
                ]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(await context.LicensePackages.AnyAsync(x => x.PreviousPackageId == sourcePackageId));
        Assert.True(await context.LicensedProducts.AnyAsync(x => x.Id == productId));
    }

    [Fact]
    public async Task UpdatePackageBelowActiveSeatCount_IsRejected()
    {
        await using var context = CreateDbContext();
        var (productId, purchaseId, packageId) = await SeedPackageAsync(context, quantity: 2);
        await SeedSeatAsync(context, packageId, "Ada", "ada@test.local");
        await SeedSeatAsync(context, packageId, "Grace", "grace@test.local");

        var service = new LicensePackageService(context, new FakeSecretProtector());
        var result = await service.UpdateAsync(
            new UpdateLicensePackageRequest(
                packageId,
                purchaseId,
                productId,
                LicenseType.NamedUser,
                1,
                null,
                null,
                false,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                true,
                LicensePackageStatus.Active,
                null,
                "tester",
                null,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, (await context.LicensePackages.FindAsync(packageId))!.Quantity);
    }

    [Fact]
    public async Task AssignSeatToInactivePackage_IsRejected()
    {
        await using var context = CreateDbContext();
        var (_, _, packageId) = await SeedPackageAsync(
            context,
            quantity: 1,
            isActive: false,
            status: LicensePackageStatus.Expired);

        var service = new LicenseSeatAssignmentService(context);
        var result = await service.AssignAsync(
            new AssignLicenseSeatRequest(
                packageId,
                new LicenseSeatPersonInput(null, "Ada", null, null, "ada@test.local", null, null, null),
                new DateOnly(2026, 9, 9),
                null,
                null,
                Actor),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(context.LicenseSeatAssignments);
    }

    [Fact]
    public async Task AssignSeatToNonNamedUserPackage_IsRejected()
    {
        await using var context = CreateDbContext();
        var (_, _, packageId) = await SeedPackageAsync(
            context, quantity: 2, licenseType: LicenseType.Concurrent);

        var service = new LicenseSeatAssignmentService(context);
        var result = await service.AssignAsync(
            new AssignLicenseSeatRequest(
                packageId,
                new LicenseSeatPersonInput("user-1", "Ada", null, null, "ada@test.local", null, null, null),
                null,
                null,
                null,
                Actor),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Empty(context.LicenseSeatAssignments);
    }

    [Fact]
    public async Task ReleaseDateBeforeAssignmentDate_IsRejected()
    {
        await using var context = CreateDbContext();
        var (_, _, packageId) = await SeedPackageAsync(context, quantity: 1);
        var seatId = await SeedSeatAsync(context, packageId, "Ada", "ada@test.local", new DateOnly(2026, 9, 9));

        var service = new LicenseSeatAssignmentService(context);
        var result = await service.ReleaseAsync(
            new ReleaseLicenseSeatRequest(
                seatId,
                new DateOnly(2026, 9, 8),
                null,
                Actor),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LicenseSeatAssignmentStatus.Active,
            (await context.LicenseSeatAssignments.FindAsync(seatId))!.Status);
    }

    [Fact]
    public async Task TransferDateBeforeAssignmentDate_IsRejected()
    {
        await using var context = CreateDbContext();
        var (_, _, packageId) = await SeedPackageAsync(context, quantity: 1);
        var seatId = await SeedSeatAsync(context, packageId, "Ada", "ada@test.local", new DateOnly(2026, 9, 9));

        var service = new LicenseSeatAssignmentService(context);
        var result = await service.TransferAsync(
            new TransferLicenseSeatRequest(
                seatId,
                new LicenseSeatPersonInput(null, "Grace", null, null, "grace@test.local", null, null, null),
                new DateOnly(2026, 9, 8),
                null,
                Actor),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Single(context.LicenseSeatAssignments);
        Assert.Equal(
            LicenseSeatAssignmentStatus.Active,
            (await context.LicenseSeatAssignments.FindAsync(seatId))!.Status);
    }

    [Fact]
    public async Task NamedUserFulfillment_MarksTheFulfilledUserAsFulfilled()
    {
        await using var context = CreateDbContext();
        var (productId, _, _) = await SeedPackageAsync(context, quantity: 1);
        var request = await SeedRequestAsync(context, productId, LicenseRequestItemStatus.Approved);
        var requestItem = request.Items.Single();

        var service = new LicenseRequestFulfillmentService(context);
        var result = await service.ConvertToPurchaseAsync(
            new ConvertLicenseRequestItemsRequest(
                null,
                new ConvertFulfillmentNewPurchaseInput(
                    LicensePurchaseType.DirectPurchase,
                    "Fulfillment",
                    null,
                    new DateOnly(2026, 9, 9),
                    null,
                    null,
                    null,
                    "TRY",
                    false,
                    null),
                [new ConvertFulfillmentLineInput(requestItem.Id, 1, [requestItem.Users.Single().Id])],
                [new ConvertFulfillmentPackageDefaultsInput(productId, LicenseType.NamedUser, null, null, false)],
                null,
                "tester",
                null,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = await context.LicenseRequestItemUsers.SingleAsync(x => x.RequestItemId == requestItem.Id);
        Assert.Equal(LicenseRequestItemUserStatus.Fulfilled, user.Status);
        var seat = await context.LicenseSeatAssignments.SingleAsync(x => x.SourceRequestItemId == requestItem.Id);
        Assert.Equal(user.AdObjectId, seat.AdObjectId);
        Assert.Equal(LicenseSeatAssignmentStatus.Active, seat.Status);
    }

    private static LicenseSeatActorContext Actor => new(null, "tester", null, null);

    private static LicenseRequestItemUserInput BuildUser(
        string id,
        LicenseRequestItemUserStatus status) =>
        new(id, id, $"{id}@test.local", id, "IT", null, $"{id}@test.local", null, status);

    private static async Task<LicenseRequest> SeedRequestAsync(
        AppDbContext context,
        Guid productId,
        LicenseRequestItemStatus status,
        int fulfilled = 0)
    {
        var request = new LicenseRequest
        {
            RequestSource = LicenseRequestSource.Email,
            RequestDate = new DateOnly(2026, 9, 9),
            RequesterUnitDisplayName = "IT",
            RequesterUnitDistinguishedName = "OU=IT,DC=test",
            RequesterUnitObjectGuid = "ou-guid",
            Status = fulfilled > 0 ? LicenseRequestStatus.Fulfilled : LicenseRequestStatus.InReview,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
            Items =
            [
                new LicenseRequestItem
                {
                    ProductId = productId,
                    RequestedQuantity = 1,
                    ApprovedQuantity = 1,
                    FulfilledQuantity = fulfilled,
                    Status = status,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "seed",
                    Users =
                    [
                        new LicenseRequestItemUser
                        {
                            AdObjectId = "user-1",
                            DisplayName = "User One",
                            Mail = "user-1@test.local",
                            Status = fulfilled > 0
                                ? LicenseRequestItemUserStatus.Fulfilled
                                : LicenseRequestItemUserStatus.Approved,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "seed",
                        },
                    ],
                },
            ],
        };
        context.LicenseRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    private static async Task<(Guid ProductId, Guid PurchaseId, Guid PackageId)> SeedPackageAsync(
        AppDbContext context,
        int quantity,
        bool isActive = true,
        LicensePackageStatus status = LicensePackageStatus.Active,
        LicenseType licenseType = LicenseType.NamedUser)
    {
        var category = new LicenseProductCategory
        {
            Name = $"Category-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        var product = new LicensedProduct
        {
            Name = $"Product-{Guid.NewGuid():N}",
            Category = category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        var purchase = new LicensePurchase
        {
            PurchaseType = LicensePurchaseType.DirectPurchase,
            Title = $"Purchase-{Guid.NewGuid():N}",
            Status = LicensePurchaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        var package = new LicensePackage
        {
            Product = product,
            Purchase = purchase,
            LicenseType = licenseType,
            Quantity = quantity,
            IsActive = isActive,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicensePackages.Add(package);
        await context.SaveChangesAsync();
        return (product.Id, purchase.Id, package.Id);
    }

    private static async Task<Guid> SeedSeatAsync(
        AppDbContext context,
        Guid packageId,
        string displayName,
        string mail,
        DateOnly? assignedDate = null)
    {
        var seat = new LicenseSeatAssignment
        {
            PackageId = packageId,
            DisplayName = displayName,
            Mail = mail,
            AssignedDate = assignedDate ?? new DateOnly(2026, 1, 1),
            Status = LicenseSeatAssignmentStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicenseSeatAssignments.Add(seat);
        await context.SaveChangesAsync();
        return seat.Id;
    }

    private static AppDbContext CreateDbContext()
    {
        var (_, context) = SqliteTestDbContextFactory.CreateAsync().GetAwaiter().GetResult();
        return context;
    }
}
