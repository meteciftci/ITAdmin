using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services.LicenseManagement;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicenseManagementServiceTests
{
    [Fact]
    public void PermissionCodes_ContainsLicenseManagementPhase1Permissions()
    {
        var codes = new[]
        {
            PermissionCodes.LicenseManagement.View,
            PermissionCodes.LicenseManagement.ManageCatalog,
            PermissionCodes.LicenseManagement.ManageAcquisitions,
            PermissionCodes.LicenseManagement.ViewReports,
            PermissionCodes.LicenseManagement.ManageSettings
        };

        foreach (var code in codes)
        {
            Assert.False(string.IsNullOrWhiteSpace(code));
            Assert.StartsWith("LicenseManagement.", code, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task CreateCompanyAsync_WithoutName_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicenseCompanyService(context);

        var result = await service.CreateAsync(
            new CreateLicenseCompanyRequest(
                "",
                null, null, null, null, null, null, null, null, null, null, null, true,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCompanyAsync_WithInvalidEmail_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicenseCompanyService(context);

        var result = await service.CreateAsync(
            new CreateLicenseCompanyRequest(
                "Acme Corp",
                null, null, "not-an-email", null, null, null, null, null, null, null, null, true,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("email", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateProductAsync_WithoutName_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicensedProductService(context);

        var result = await service.CreateAsync(
            new CreateLicensedProductRequest(
                "",
                null, null, null, null, true, null,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateProductAsync_DuplicateActiveName_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        context.LicensedProducts.Add(new Domain.Entities.LicensedProduct
        {
            Name = "Microsoft Office",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await context.SaveChangesAsync();

        var service = new LicensedProductService(context);
        var result = await service.CreateAsync(
            new CreateLicensedProductRequest(
                "microsoft office",
                null, null, null, null, true, null,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("same name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithoutTitle_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicensePurchaseService(context);

        var result = await service.CreateAsync(
            new CreateLicensePurchaseRequest(
                LicensePurchaseType.Tender,
                "",
                null, null, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null,
                LicensePurchaseStatus.Draft,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("title", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePurchaseAsync_InvalidContractDateRange_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicensePurchaseService(context);

        var result = await service.CreateAsync(
            new CreateLicensePurchaseRequest(
                LicensePurchaseType.DirectPurchase,
                "Office License Purchase",
                null, null, null, null, null, null, null, null, null, null, null,
                new DateOnly(2026, 12, 31),
                new DateOnly(2026, 1, 1),
                null, null, null, null, null, null,
                LicensePurchaseStatus.Draft,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("contract", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePackageAsync_InvalidQuantity_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var purchaseId = await SeedPurchaseAsync(context);
        var productId = await SeedProductAsync(context);

        var service = new LicensePackageService(context);
        var result = await service.CreateAsync(
            new CreateLicensePackageRequest(
                purchaseId,
                productId,
                LicenseType.NamedUser,
                0,
                null, null, false, false, null, null, null, null, null, null, true,
                LicensePackageStatus.Active,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("quantity", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePackageAsync_InvalidPurchase_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);

        var service = new LicensePackageService(context);
        var result = await service.CreateAsync(
            new CreateLicensePackageRequest(
                Guid.NewGuid(),
                productId,
                LicenseType.NamedUser,
                5,
                null, null, false, false, null, null, null, null, null, null, true,
                LicensePackageStatus.Active,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("purchase", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePackageAsync_InvalidProduct_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var purchaseId = await SeedPurchaseAsync(context);

        var service = new LicensePackageService(context);
        var result = await service.CreateAsync(
            new CreateLicensePackageRequest(
                purchaseId,
                Guid.NewGuid(),
                LicenseType.NamedUser,
                5,
                null, null, false, false, null, null, null, null, null, null, true,
                LicensePackageStatus.Active,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("product", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNoRowExists_ReturnsDefaults()
    {
        await using var context = CreateDbContext();
        var service = new LicenseManagementSettingsService(context);

        var settings = await service.GetSettingsAsync(CancellationToken.None);

        Assert.Equal("TRY", settings.DefaultCurrency);
        Assert.False(settings.DefaultVatIncluded);
        Assert.Equal(60, settings.DefaultRenewalReminderDays);
        Assert.Null(settings.DefaultRenewalRecipients);
        Assert.Null(settings.DefaultRenewalCcRecipients);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithInvalidRenewalReminderDays_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicenseManagementSettingsService(context);

        var result = await service.UpdateSettingsAsync(
            new UpdateLicenseManagementSettingsRequest(
                "TRY",
                false,
                0,
                null,
                null,
                null,
                null,
                "tester",
                null,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("renewal reminder", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsCorrectCounts()
    {
        await using var context = CreateDbContext();
        context.LicenseCompanies.Add(new Domain.Entities.LicenseCompany
        {
            Name = "Vendor A",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        context.LicensedProducts.Add(new Domain.Entities.LicensedProduct
        {
            Name = "Product A",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        context.LicensedProducts.Add(new Domain.Entities.LicensedProduct
        {
            Name = "Product B",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        var purchase = new Domain.Entities.LicensePurchase
        {
            PurchaseType = LicensePurchaseType.Tender,
            Title = "Purchase 1",
            Status = LicensePurchaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        context.LicensePurchases.Add(purchase);
        await context.SaveChangesAsync();

        context.LicensePackages.Add(new Domain.Entities.LicensePackage
        {
            PurchaseId = purchase.Id,
            ProductId = context.LicensedProducts.First().Id,
            LicenseType = LicenseType.NamedUser,
            Quantity = 10,
            Status = LicensePackageStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        context.LicensePackages.Add(new Domain.Entities.LicensePackage
        {
            PurchaseId = purchase.Id,
            ProductId = context.LicensedProducts.First().Id,
            LicenseType = LicenseType.Concurrent,
            Quantity = 5,
            Status = LicensePackageStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await context.SaveChangesAsync();

        var service = new LicenseManagementOverviewService(context);
        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(1, summary.CompanyCount);
        Assert.Equal(1, summary.ActiveProductCount);
        Assert.Equal(1, summary.PurchaseCount);
        Assert.Equal(2, summary.PackageCount);
        Assert.Equal(15, summary.TotalLicenseQuantity);
    }

    private static async Task<Guid> SeedPurchaseAsync(AppDbContext context)
    {
        var purchase = new Domain.Entities.LicensePurchase
        {
            PurchaseType = LicensePurchaseType.DirectPurchase,
            Title = "Test Purchase",
            Status = LicensePurchaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        context.LicensePurchases.Add(purchase);
        await context.SaveChangesAsync();
        return purchase.Id;
    }

    private static async Task<Guid> SeedProductAsync(AppDbContext context)
    {
        var product = new Domain.Entities.LicensedProduct
        {
            Name = "Test Product",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        context.LicensedProducts.Add(product);
        await context.SaveChangesAsync();
        return product.Id;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
