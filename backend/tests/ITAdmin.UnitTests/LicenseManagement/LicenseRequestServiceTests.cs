using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services.LicenseManagement;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicenseRequestServiceTests
{
    [Fact]
    public void PermissionCodes_ContainsLicenseRequestPermissions()
    {
        Assert.Equal("LicenseManagement.ManageRequests", PermissionCodes.LicenseManagement.ManageRequests);
        Assert.Equal("Directory.Users.Lookup", PermissionCodes.Directory.Users.Lookup);
        Assert.Equal("Directory.OrganizationalUnits.Lookup", PermissionCodes.Directory.OrganizationalUnits.Lookup);
    }

    [Fact]
    public async Task CreateAsync_WithMultiProductMultiUser_Succeeds()
    {
        await using var context = CreateDbContext();
        var product1 = await SeedProductAsync(context, "Photoshop");
        var product2 = await SeedProductAsync(context, "OSKA");
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            BuildCreateRequest(
                product1,
                product2,
                ("user-1", "hakan"),
                ("user-2", "mete")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Request);
        Assert.Equal(2, result.Request.Items.Count);
        Assert.Equal(2, result.Request.Items.First(x => x.ProductId == product1).Users.Count);
        Assert.Equal(2, result.Request.Items.First(x => x.ProductId == product2).Users.Count);
    }

    [Fact]
    public async Task CreateAsync_SameUserAcrossDifferentProducts_Succeeds()
    {
        await using var context = CreateDbContext();
        var product1 = await SeedProductAsync(context, "Photoshop");
        var product2 = await SeedProductAsync(context, "OSKA");
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            BuildCreateRequest(
                product1,
                product2,
                ("shared-user", "hakan")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Request!.Items.Count);
        Assert.All(result.Request.Items, item => Assert.Single(item.Users));
    }

    [Fact]
    public async Task CreateAsync_DuplicateProduct_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            new CreateLicenseRequestRequest(
                "LT-2026-001",
                LicenseRequestSource.Email,
                new DateOnly(2026, 6, 29),
                null, null, null,
                BuildRequesterUnit(),
                null, null,
                LicenseRequestStatus.Pending,
                null, "TRY", false, null,
                [
                    BuildItem(productId, ("user-1", "hakan")),
                    BuildItem(productId, ("user-2", "mete")),
                ],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseRequestRules.DuplicateProductMessage, result.Message);
    }

    [Fact]
    public async Task CreateAsync_DuplicateUserInSameItem_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            new CreateLicenseRequestRequest(
                "LT-2026-002",
                LicenseRequestSource.Email,
                new DateOnly(2026, 6, 29),
                null, null, null,
                BuildRequesterUnit(),
                null, null,
                LicenseRequestStatus.Pending,
                null, "TRY", false, null,
                [
                    new LicenseRequestItemInput(
                        productId,
                        1000, "TRY", false, null,
                        LicenseRequestItemStatus.Pending,
                        [
                            BuildUser("same-user", "hakan"),
                            BuildUser("same-user", "hakan"),
                        ]),
                ],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseRequestRules.DuplicateUserMessage, result.Message);
    }

    [Fact]
    public async Task CreateAsync_WithoutItems_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            new CreateLicenseRequestRequest(
                "LT-2026-003",
                LicenseRequestSource.Email,
                new DateOnly(2026, 6, 29),
                null, null, null,
                BuildRequesterUnit(),
                null, null,
                LicenseRequestStatus.Pending,
                null, "TRY", false, null,
                [],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("product item", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_ItemWithoutUsers_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            new CreateLicenseRequestRequest(
                "LT-2026-004",
                LicenseRequestSource.Email,
                new DateOnly(2026, 6, 29),
                null, null, null,
                BuildRequesterUnit(),
                null, null,
                LicenseRequestStatus.Pending,
                null, "TRY", false, null,
                [
                    new LicenseRequestItemInput(
                        productId,
                        1000, "TRY", false, null,
                        LicenseRequestItemStatus.Pending,
                        []),
                ],
                null, "tester", null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("user", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_PassiveProduct_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context, isActive: false);
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("passive", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_SetsRequestedQuantityFromUserCount()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            BuildCreateRequest(
                productId,
                ("user-1", "hakan"),
                ("user-2", "mete"),
                ("user-3", "murat")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = result.Request!.Items.Single();
        Assert.Equal(3, item.RequestedQuantity);
        Assert.Equal(3, item.ApprovedQuantity);
        Assert.Equal(0, item.FulfilledQuantity);
    }

    [Fact]
    public async Task CreateAsync_FulfilledStatus_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var request = BuildCreateRequest(productId, ("user-1", "hakan")) with
        {
            Status = LicenseRequestStatus.Fulfilled
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("status", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_WithoutRequesterUnit_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var request = BuildCreateRequest(productId, ("user-1", "hakan")) with
        {
            RequesterUnit = new LicenseRequestOuSnapshot("", "Unit", "OU=Unit,DC=test"),
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("requester unit", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_OfficialLetterWithoutEbys_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var request = BuildCreateRequest(productId, ("user-1", "hakan")) with
        {
            EbysNumber = null,
            EbysDate = null,
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("EBYS", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_CorporateRequestSystemWithoutExternalNumber_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var request = BuildCreateRequest(productId, ("user-1", "hakan")) with
        {
            RequestSource = LicenseRequestSource.CorporateRequestSystem,
            ExternalRequestNumber = null,
            EbysNumber = null,
            EbysDate = null,
        };

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("external request", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_EmailSource_DoesNotRequireEbysOrExternalNumber()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")) with
            {
                RequestSource = LicenseRequestSource.Email,
                ExternalRequestNumber = null,
                EbysNumber = null,
                EbysDate = null,
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Request!.ExternalRequestNumber);
        Assert.Null(result.Request.EbysNumber);
        Assert.Null(result.Request.EbysDate);
    }

    [Fact]
    public async Task CreateAsync_NormalizesIrrelevantSourceFields()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")) with
            {
                RequestSource = LicenseRequestSource.Email,
                ExternalRequestNumber = "EXT-1",
                EbysNumber = "EBYS-1",
                EbysDate = new DateOnly(2026, 6, 29),
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Request!.ExternalRequestNumber);
        Assert.Null(result.Request.EbysNumber);
        Assert.Null(result.Request.EbysDate);
    }

    [Fact]
    public async Task GetListAsync_FilterByStatus_ReturnsMatchingItems()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);
        await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")) with { Status = LicenseRequestStatus.Pending },
            CancellationToken.None);
        await service.CreateAsync(
            BuildCreateRequest(productId, ("user-2", "mete")) with
            {
                RequestNumber = "LT-2026-010",
                Status = LicenseRequestStatus.Draft
            },
            CancellationToken.None);

        var result = await service.GetListAsync(
            new LicenseRequestListQuery(null, LicenseRequestStatus.Draft, null, null, null, null, null, 1, 20),
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(LicenseRequestStatus.Draft, result.Items.First().Status);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsItemAndUserSnapshotsWithoutHeaderUser()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);
        var created = await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")),
            CancellationToken.None);

        var detail = await service.GetByIdAsync(created.Request!.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Bilgi İşlem", detail.RequesterUnitDisplayName);
        Assert.Single(detail.Items);
        Assert.Equal("Photoshop", detail.Items[0].ProductName);
        Assert.Single(detail.Items[0].Users);
        Assert.Equal("hakan", detail.Items[0].Users[0].SamAccountName);
    }

    [Fact]
    public async Task CreateAsync_WritesAuditLog()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")),
            CancellationToken.None);

        var audit = await context.AuditLogs.SingleAsync();
        Assert.Equal("Create", audit.Action);
        Assert.Equal("LicenseRequest", audit.EntityName);
        Assert.Contains("License request created", audit.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LicenseRequestSource.OfficialLetter, null, "EBYS-1", "2026-06-29", null, "EBYS-1")]
    [InlineData(LicenseRequestSource.CorporateRequestSystem, "EXT-1", "EBYS-1", "2026-06-29", "EXT-1", null)]
    [InlineData(LicenseRequestSource.Email, "EXT-1", "EBYS-1", "2026-06-29", null, null)]
    public void NormalizeSourceFields_ClearsIrrelevantValues(
        LicenseRequestSource source,
        string? externalRequestNumber,
        string? ebysNumber,
        string? ebysDateText,
        string? expectedExternal,
        string? expectedEbys)
    {
        DateOnly? ebysDate = ebysDateText is null ? null : DateOnly.Parse(ebysDateText);

        var normalized = LicenseRequestRules.NormalizeSourceFields(
            source,
            externalRequestNumber,
            ebysNumber,
            ebysDate);

        Assert.Equal(expectedExternal, normalized.ExternalRequestNumber);
        Assert.Equal(expectedEbys, normalized.EbysNumber);
        if (source == LicenseRequestSource.OfficialLetter)
        {
            Assert.Equal(ebysDate, normalized.EbysDate);
        }
        else
        {
            Assert.Null(normalized.EbysDate);
        }
    }

    private static CreateLicenseRequestRequest BuildCreateRequest(
        Guid productId,
        params (string AdObjectId, string SamAccountName)[] users) =>
        new(
            "LT-2026-001",
            LicenseRequestSource.OfficialLetter,
            new DateOnly(2026, 6, 29),
            null, "EBYS-1", new DateOnly(2026, 6, 29),
            BuildRequesterUnit(),
            "Manager Name",
            "Test request",
            LicenseRequestStatus.Pending,
            10000, "TRY", false, "Cost note",
            [BuildItem(productId, users)],
            null, "tester", null, null);

    private static CreateLicenseRequestRequest BuildCreateRequest(
        Guid productId1,
        Guid productId2,
        params (string AdObjectId, string SamAccountName)[] usersPerProduct) =>
        new(
            "LT-2026-001",
            LicenseRequestSource.OfficialLetter,
            new DateOnly(2026, 6, 29),
            null, "EBYS-1", new DateOnly(2026, 6, 29),
            BuildRequesterUnit(),
            null, null,
            LicenseRequestStatus.Pending,
            null, "TRY", false, null,
            [
                BuildItem(productId1, usersPerProduct),
                BuildItem(productId2, usersPerProduct),
            ],
            null, "tester", null, null);

    private static LicenseRequestItemInput BuildItem(
        Guid productId,
        params (string AdObjectId, string SamAccountName)[] users) =>
        new(
            productId,
            1000, "TRY", false, "Justification",
            LicenseRequestItemStatus.Pending,
            users.Select(user => BuildUser(user.AdObjectId, user.SamAccountName)).ToList());

    private static LicenseRequestItemUserInput BuildUser(string adObjectId, string samAccountName) =>
        new(
            adObjectId,
            samAccountName,
            $"{samAccountName}@example.com",
            samAccountName,
            "IT",
            "Engineer",
            $"{samAccountName}@example.com",
            null,
            LicenseRequestItemUserStatus.Pending);

    private static LicenseRequestOuSnapshot BuildRequesterUnit() =>
        new(
            "ou-guid-1",
            "Bilgi İşlem",
            "OU=Bilgi Islem,DC=example,DC=local");

    private static async Task<Guid> SeedProductAsync(
        AppDbContext context,
        string name = "Photoshop",
        bool isActive = true)
    {
        var category = new Domain.Entities.LicenseProductCategory
        {
            Name = "Grafik Tasarım",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
        };
        context.LicenseProductCategories.Add(category);
        await context.SaveChangesAsync();

        var product = new Domain.Entities.LicensedProduct
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
