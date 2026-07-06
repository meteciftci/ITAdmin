using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services.LicenseManagement;
using ITAdmin.UnitTests.TestInfrastructure;

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
                LicenseRequestSource.Email,
                new DateOnly(2026, 6, 29),
                null, null, null,
                BuildRequesterUnit(),
                null, null,
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
                LicenseRequestSource.Email,
                new DateOnly(2026, 6, 29),
                null, null, null,
                BuildRequesterUnit(),
                null, null,
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
                LicenseRequestSource.Email,
                new DateOnly(2026, 6, 29),
                null, null, null,
                BuildRequesterUnit(),
                null, null,
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
                LicenseRequestSource.Email,
                new DateOnly(2026, 6, 29),
                null, null, null,
                BuildRequesterUnit(),
                null, null,
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
    public async Task CreateAsync_DerivesPendingStatusFromNewItems()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);

        var result = await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(LicenseRequestStatus.Pending, result.Request!.Status);
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
    public async Task GetListAsync_FilterByDerivedStatus_ReturnsMatchingItems()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);
        await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")),
            CancellationToken.None);
        await service.CreateAsync(
            BuildCreateRequest(productId, ("user-2", "mete")),
            CancellationToken.None);

        // Newly created requests derive to Pending (all items Pending).
        var pending = await service.GetListAsync(
            new LicenseRequestListQuery(null, LicenseRequestStatus.Pending, null, null, null, null, null, 1, 20),
            CancellationToken.None);
        Assert.Equal(2, pending.TotalCount);
        Assert.All(pending.Items, item => Assert.Equal(LicenseRequestStatus.Pending, item.Status));

        var fulfilled = await service.GetListAsync(
            new LicenseRequestListQuery(null, LicenseRequestStatus.Fulfilled, null, null, null, null, null, 1, 20),
            CancellationToken.None);
        Assert.Equal(0, fulfilled.TotalCount);
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

    [Fact]
    public async Task UpdateAsync_NonExistentRequest_ReturnsFailure()
    {
        await using var context = CreateDbContext();
        var service = new LicenseRequestService(context);

        var result = await service.UpdateAsync(
            BuildUpdateRequest(Guid.NewGuid(), Guid.NewGuid(), ("user-1", "hakan")),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_ChangesUsers_UpdatesQuantitiesAndWritesAudit()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);
        var created = await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")),
            CancellationToken.None);

        var result = await service.UpdateAsync(
            BuildUpdateRequest(
                created.Request!.Id,
                productId,
                ("user-1", "hakan"),
                ("user-2", "mete")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = result.Request!.Items.Single();
        Assert.Equal(2, item.Users.Count);
        Assert.Equal(2, item.RequestedQuantity);

        var updateAudit = await context.AuditLogs.SingleAsync(x => x.Action == "Update");
        Assert.Equal("LicenseRequest", updateAudit.EntityName);
        Assert.Contains("License request updated", updateAudit.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateProduct_ReturnsValidationFailure()
    {
        await using var context = CreateDbContext();
        var productId = await SeedProductAsync(context);
        var service = new LicenseRequestService(context);
        var created = await service.CreateAsync(
            BuildCreateRequest(productId, ("user-1", "hakan")),
            CancellationToken.None);

        var request = BuildUpdateRequest(created.Request!.Id, productId, ("user-1", "hakan")) with
        {
            Items =
            [
                BuildItem(productId, ("user-1", "hakan")),
                BuildItem(productId, ("user-2", "mete")),
            ],
        };

        var result = await service.UpdateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseRequestRules.DuplicateProductMessage, result.Message);
    }

    private static UpdateLicenseRequestRequest BuildUpdateRequest(
        Guid requestId,
        Guid productId,
        params (string AdObjectId, string SamAccountName)[] users) =>
        new(
            requestId,
            LicenseRequestSource.OfficialLetter,
            new DateOnly(2026, 6, 29),
            null, "EBYS-1", new DateOnly(2026, 6, 29),
            BuildRequesterUnit(),
            "Manager Name",
            "Updated request",
            10000, "TRY", false, "Cost note",
            [BuildItem(productId, users)],
            null, "tester", null, null);

    private static CreateLicenseRequestRequest BuildCreateRequest(
        Guid productId,
        params (string AdObjectId, string SamAccountName)[] users) =>
        new(
            LicenseRequestSource.OfficialLetter,
            new DateOnly(2026, 6, 29),
            null, "EBYS-1", new DateOnly(2026, 6, 29),
            BuildRequesterUnit(),
            "Manager Name",
            "Test request",
            10000, "TRY", false, "Cost note",
            [BuildItem(productId, users)],
            null, "tester", null, null);

    private static CreateLicenseRequestRequest BuildCreateRequest(
        Guid productId1,
        Guid productId2,
        params (string AdObjectId, string SamAccountName)[] usersPerProduct) =>
        new(
            LicenseRequestSource.OfficialLetter,
            new DateOnly(2026, 6, 29),
            null, "EBYS-1", new DateOnly(2026, 6, 29),
            BuildRequesterUnit(),
            null, null,
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
        // Relational SQLite (not the EF in-memory provider) so cascade deletes and the item
        // replacement performed by UpdateAsync behave like production PostgreSQL. The in-memory
        // provider cannot model the delete-and-reinsert of request items.
        var (_, context) = SqliteTestDbContextFactory.CreateAsync().GetAwaiter().GetResult();
        return context;
    }
}
