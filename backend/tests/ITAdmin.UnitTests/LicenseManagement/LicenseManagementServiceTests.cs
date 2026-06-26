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
    public async Task CreateAcquisitionAsync_WithoutTitle_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicenseAcquisitionService(context);

        var result = await service.CreateAsync(
            new CreateLicenseAcquisitionRequest(
                LicenseAcquisitionType.Tender,
                "",
                null, null, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null,
                LicenseAcquisitionStatus.Draft,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("title", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAcquisitionAsync_InvalidContractDateRange_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicenseAcquisitionService(context);

        var result = await service.CreateAsync(
            new CreateLicenseAcquisitionRequest(
                LicenseAcquisitionType.DirectPurchase,
                "Office License Purchase",
                null, null, null, null, null, null, null, null, null, null, null,
                new DateOnly(2026, 12, 31),
                new DateOnly(2026, 1, 1),
                null, null, null, null, null, null,
                LicenseAcquisitionStatus.Draft,
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("contract", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePackageAsync_InvalidQuantity_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var acquisitionId = await SeedAcquisitionAsync(context);
        var productId = await SeedProductAsync(context);

        var service = new LicensePackageService(context);
        var result = await service.CreateAsync(
            new CreateLicensePackageRequest(
                acquisitionId,
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
    public async Task CreatePackageAsync_InvalidAcquisition_ReturnsValidationFailure()
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
        Assert.Contains("acquisition", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePackageAsync_InvalidProduct_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var acquisitionId = await SeedAcquisitionAsync(context);

        var service = new LicensePackageService(context);
        var result = await service.CreateAsync(
            new CreateLicensePackageRequest(
                acquisitionId,
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
        var acquisition = new Domain.Entities.LicenseAcquisition
        {
            AcquisitionType = LicenseAcquisitionType.Tender,
            Title = "Acquisition 1",
            Status = LicenseAcquisitionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        context.LicenseAcquisitions.Add(acquisition);
        await context.SaveChangesAsync();

        context.LicensePackages.Add(new Domain.Entities.LicensePackage
        {
            AcquisitionId = acquisition.Id,
            ProductId = context.LicensedProducts.First().Id,
            LicenseType = LicenseType.NamedUser,
            Quantity = 10,
            Status = LicensePackageStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        context.LicensePackages.Add(new Domain.Entities.LicensePackage
        {
            AcquisitionId = acquisition.Id,
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
        Assert.Equal(1, summary.AcquisitionCount);
        Assert.Equal(2, summary.PackageCount);
        Assert.Equal(15, summary.TotalLicenseQuantity);
    }

    private static async Task<Guid> SeedAcquisitionAsync(AppDbContext context)
    {
        var acquisition = new Domain.Entities.LicenseAcquisition
        {
            AcquisitionType = LicenseAcquisitionType.DirectPurchase,
            Title = "Test Acquisition",
            Status = LicenseAcquisitionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };
        context.LicenseAcquisitions.Add(acquisition);
        await context.SaveChangesAsync();
        return acquisition.Id;
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
