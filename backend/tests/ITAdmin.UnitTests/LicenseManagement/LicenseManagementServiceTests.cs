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
            PermissionCodes.LicenseManagement.ManagePurchases,
            PermissionCodes.LicenseManagement.ManageRequests,
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
                null, null, null, null, null, null, null, true,
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
                null, "not-an-email", null, null, null, null, null, true,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("email", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateProductAsync_WithoutName_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new LicensedProductService(context);

        var result = await service.CreateAsync(
            new CreateLicensedProductRequest(
                "",
                null, categoryId, null, true,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateProductAsync_DuplicateActiveName_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var categoryId = await SeedCategoryAsync(context);
        context.LicensedProducts.Add(new Domain.Entities.LicensedProduct
        {
            Name = "Microsoft Office",
            CategoryId = categoryId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await context.SaveChangesAsync();

        var service = new LicensedProductService(context);
        var result = await service.CreateAsync(
            new CreateLicensedProductRequest(
                "microsoft office",
                null, categoryId, null, true,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("same name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateProductAsync_WithBrandAndCategory_Succeeds()
    {
        await using var context = CreateDbContext();
        var categoryId = await SeedCategoryAsync(context, "Grafik Tasarım");
        var service = new LicensedProductService(context);

        var result = await service.CreateAsync(
            new CreateLicensedProductRequest(
                "Photoshop",
                "Adobe", categoryId, "Design suite", true,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Product);
        Assert.Equal("Adobe", result.Product.Brand);
        Assert.Equal("Grafik Tasarım", result.Product.CategoryName);
    }

    [Fact]
    public async Task CreateProductAsync_WithPassiveCategory_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var categoryId = await SeedCategoryAsync(context, "Legacy", isActive: false);
        var service = new LicensedProductService(context);

        var result = await service.CreateAsync(
            new CreateLicensedProductRequest(
                "Photoshop",
                null, categoryId, null, true,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Passive", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCategoryAsync_DuplicateActiveName_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        await SeedCategoryAsync(context, "Grafik Tasarım");
        var service = new LicenseProductCategoryService(context);

        var result = await service.CreateAsync(
            new CreateLicenseProductCategoryRequest(
                "grafik tasarım",
                null,
                true,
                null,
                "tester",
                null,
                null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("same name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePurchaseAsync_WithDirectPurchaseAndDraft_Succeeds()
    {
        await using var context = CreateDbContext();
        var service = new LicensePurchaseService(context);

        var result = await service.CreateAsync(
            new CreateLicensePurchaseRequest(
                LicensePurchaseType.DirectPurchase,
                "Direct purchase record",
                null, null, null, null, "DT-100", null, null, null, null, null, null, null, null,
                null, null, null, null, null, null,
                LicensePurchaseStatus.Draft,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Purchase);
        Assert.Equal(LicensePurchaseType.DirectPurchase, result.Purchase.PurchaseType);
        Assert.Equal(LicensePurchaseStatus.Draft, result.Purchase.Status);
    }

    [Fact]
    public async Task CreatePackageAsync_WithNamedUserAndActiveStatus_Succeeds()
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
                10,
                null, null, false, false, null, null, null, null, null, null, true,
                LicensePackageStatus.Active,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Package);
        Assert.Equal(LicenseType.NamedUser, result.Package.LicenseType);
        Assert.Equal(LicensePackageStatus.Active, result.Package.Status);
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
    public async Task GetPackageListAsync_FilterByPurchaseId_ReturnsOnlyMatchingPackages()
    {
        await using var context = CreateDbContext();
        var purchaseId1 = await SeedPurchaseAsync(context);
        var purchaseId2 = await SeedPurchaseAsync(context);
        var productId = await SeedProductAsync(context);

        context.LicensePackages.AddRange(
            new Domain.Entities.LicensePackage
            {
                PurchaseId = purchaseId1,
                ProductId = productId,
                LicenseType = LicenseType.NamedUser,
                Quantity = 5,
                IsActive = true,
                Status = LicensePackageStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed",
            },
            new Domain.Entities.LicensePackage
            {
                PurchaseId = purchaseId2,
                ProductId = productId,
                LicenseType = LicenseType.NamedUser,
                Quantity = 3,
                IsActive = true,
                Status = LicensePackageStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed",
            });
        await context.SaveChangesAsync();

        var service = new LicensePackageService(context);
        var result = await service.GetListAsync(
            new LicensePackageListQuery(null, purchaseId1, null, null, null, 1, 20),
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(5, result.Items.First().Quantity);
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
        var categoryId = await SeedCategoryAsync(context);
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
            CategoryId = categoryId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        context.LicensedProducts.Add(new Domain.Entities.LicensedProduct
        {
            Name = "Product B",
            CategoryId = categoryId,
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

    private static async Task<Guid> SeedCategoryAsync(
        AppDbContext context,
        string name = "Genel",
        bool isActive = true)
    {
        var category = new Domain.Entities.LicenseProductCategory
        {
            Name = name,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        context.LicenseProductCategories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<Guid> SeedProductAsync(AppDbContext context)
    {
        var categoryId = await SeedCategoryAsync(context);
        var product = new Domain.Entities.LicensedProduct
        {
            Name = "Test Product",
            CategoryId = categoryId,
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
