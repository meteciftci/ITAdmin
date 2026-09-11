using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services.LicenseManagement;
using ITAdmin.UnitTests.TestInfrastructure;
using ITAdmin.UnitTests.Fakes;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicenseLifecycleServiceTests
{
    [Fact]
    public async Task CreatePackage_InvalidDateRange_IsRejected()
    {
        await using var context = CreateDbContext();
        var (purchase, product, _) = await SeedAsync(context);
        var service = new LicensePackageService(context, new FakeSecretProtector());

        var result = await service.CreateAsync(
            BuildCreatePackage(
                purchase.Id,
                product.Id,
                startDate: new DateOnly(2027, 2, 1),
                endDate: new DateOnly(2027, 1, 1)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("end date", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdatePackageStatus_ToExpired_ReleasesSeatsAndDeactivatesPackage()
    {
        await using var context = CreateDbContext();
        var (_, _, package) = await SeedAsync(context);
        var seat = new LicenseSeatAssignment
        {
            PackageId = package.Id,
            AdObjectId = "user-1",
            DisplayName = "Ada",
            AssignedDate = new DateOnly(2026, 1, 1),
            Status = LicenseSeatAssignmentStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        context.LicenseSeatAssignments.Add(seat);
        await context.SaveChangesAsync();
        var service = new LicensePackageService(context, new FakeSecretProtector());

        var result = await service.UpdateStatusAsync(
            new UpdateLicensePackageStatusRequest(
                package.Id, LicensePackageStatus.Expired, null, "tester", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updatedPackage = await context.LicensePackages.AsNoTracking().SingleAsync(x => x.Id == package.Id);
        var updatedSeat = await context.LicenseSeatAssignments.AsNoTracking().SingleAsync(x => x.Id == seat.Id);
        Assert.False(updatedPackage.IsActive);
        Assert.Equal(LicensePackageStatus.Expired, updatedPackage.Status);
        Assert.Equal(LicenseSeatAssignmentStatus.Released, updatedSeat.Status);
        Assert.NotNull(updatedSeat.ReleasedDate);
        var audit = await context.AuditLogs.AsNoTracking().SingleAsync(x => x.EntityId == package.Id.ToString());
        Assert.Equal("StatusChange", audit.Action);
        Assert.Contains("from Active to Expired", audit.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TerminalPackageAndPurchase_CannotBeReactivated()
    {
        await using var context = CreateDbContext();
        var (purchase, _, package) = await SeedAsync(context);
        purchase.Status = LicensePurchaseStatus.Archived;
        package.Status = LicensePackageStatus.Archived;
        package.IsActive = false;
        await context.SaveChangesAsync();

        var purchaseResult = await new LicensePurchaseService(context).UpdateStatusAsync(
            new UpdateLicensePurchaseStatusRequest(
                purchase.Id, LicensePurchaseStatus.Active, null, "tester", null, null),
            CancellationToken.None);
        var packageResult = await new LicensePackageService(context, new FakeSecretProtector()).UpdateStatusAsync(
            new UpdateLicensePackageStatusRequest(
                package.Id, LicensePackageStatus.Active, null, "tester", null, null),
            CancellationToken.None);

        Assert.False(purchaseResult.IsSuccess);
        Assert.False(packageResult.IsSuccess);
    }

    [Fact]
    public async Task CreatePackage_ArchivedPurchase_IsRejected()
    {
        await using var context = CreateDbContext();
        var (purchase, product, _) = await SeedAsync(context);
        purchase.Status = LicensePurchaseStatus.Archived;
        await context.SaveChangesAsync();
        var service = new LicensePackageService(context, new FakeSecretProtector());

        var result = await service.CreateAsync(
            BuildCreatePackage(purchase.Id, product.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("draft or active", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenewActivePackage_WithoutExpiringSource_IsRejected()
    {
        await using var context = CreateDbContext();
        var (_, _, source) = await SeedAsync(context);
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(
            BuildRenewal(source.Id, expireSource: false, copySeats: false),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(await context.LicensePackages.AnyAsync(x => x.PreviousPackageId == source.Id));
    }

    [Fact]
    public async Task RenewAndExpireWithoutCopy_ReleasesSourceSeats()
    {
        await using var context = CreateDbContext();
        var (_, _, source) = await SeedAsync(context);
        var seat = new LicenseSeatAssignment
        {
            PackageId = source.Id,
            AdObjectId = "user-1",
            DisplayName = "Ada",
            AssignedDate = new DateOnly(2026, 1, 1),
            Status = LicenseSeatAssignmentStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        context.LicenseSeatAssignments.Add(seat);
        await context.SaveChangesAsync();
        var service = new LicenseRequestFulfillmentService(context);

        var result = await service.ConvertToPurchaseAsync(
            BuildRenewal(source.Id, expireSource: true, copySeats: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            LicenseSeatAssignmentStatus.Released,
            (await context.LicenseSeatAssignments.AsNoTracking().SingleAsync(x => x.Id == seat.Id)).Status);
    }

    [Fact]
    public async Task SourcePackage_CannotHaveTwoRenewalSuccessors()
    {
        await using var context = CreateDbContext();
        var (purchase, product, source) = await SeedAsync(context);
        context.LicensePackages.AddRange(
            BuildSuccessor(purchase.Id, product.Id, source.Id),
            BuildSuccessor(purchase.Id, product.Id, source.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static CreateLicensePackageRequest BuildCreatePackage(
        Guid purchaseId,
        Guid productId,
        DateOnly? startDate = null,
        DateOnly? endDate = null) =>
        new(
            purchaseId, productId, LicenseType.NamedUser, 2, startDate, endDate,
            false, false, null, null, null, null, null, null, true,
            LicensePackageStatus.Active, null, "tester", null, null);

    private static ConvertLicenseRequestItemsRequest BuildRenewal(
        Guid sourcePackageId,
        bool expireSource,
        bool copySeats) =>
        new(
            null,
            new ConvertFulfillmentNewPurchaseInput(
                LicensePurchaseType.Renewal,
                "Renewal",
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
            [new ConvertFulfillmentRenewalLineInput(
                sourcePackageId,
                2,
                null,
                new DateOnly(2027, 1, 1),
                new DateOnly(2027, 12, 31),
                false,
                expireSource,
                copySeats,
                false,
                null)]);

    private static LicensePackage BuildSuccessor(Guid purchaseId, Guid productId, Guid sourceId) =>
        new()
        {
            PurchaseId = purchaseId,
            ProductId = productId,
            PreviousPackageId = sourceId,
            LicenseType = LicenseType.NamedUser,
            Quantity = 2,
            IsActive = true,
            Status = LicensePackageStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };

    private static async Task<(LicensePurchase Purchase, LicensedProduct Product, LicensePackage Package)> SeedAsync(
        AppDbContext context)
    {
        var category = new LicenseProductCategory
        {
            Name = $"Category-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        var product = new LicensedProduct
        {
            Name = $"Product-{Guid.NewGuid():N}",
            Category = category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        var purchase = new LicensePurchase
        {
            PurchaseType = LicensePurchaseType.DirectPurchase,
            Title = "Purchase",
            Status = LicensePurchaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        var package = new LicensePackage
        {
            Purchase = purchase,
            Product = product,
            LicenseType = LicenseType.NamedUser,
            Quantity = 2,
            IsActive = true,
            Status = LicensePackageStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        context.LicensePackages.Add(package);
        await context.SaveChangesAsync();
        return (purchase, product, package);
    }

    private static AppDbContext CreateDbContext()
    {
        var (_, context) = SqliteTestDbContextFactory.CreateAsync().GetAwaiter().GetResult();
        return context;
    }
}
