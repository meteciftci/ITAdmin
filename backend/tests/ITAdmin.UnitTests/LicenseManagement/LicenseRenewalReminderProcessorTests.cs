using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Application.Notifications;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using ITAdmin.Persistence.Services.LicenseManagement;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicenseRenewalReminderProcessorTests
{
    [Fact]
    public async Task RunAsync_QueuesOneEmailPerDistinctRecipient_AndIsIdempotent()
    {
        await using var context = CreateDbContext();
        var package = await SeedDuePackageAsync(context);
        await SeedSettingsAndTemplateAsync(
            context,
            "owner@example.com; owner@example.com",
            "audit@example.com");
        var processor = new LicenseRenewalReminderProcessor(context, new NotificationTemplateRenderer());

        var first = await processor.RunAsync();
        var second = await processor.RunAsync();

        Assert.Equal(1, first.DuePackageCount);
        Assert.Equal(2, first.QueuedCount);
        Assert.Equal(0, first.AlreadyQueuedCount);
        Assert.Equal(0, second.QueuedCount);
        Assert.Equal(2, second.AlreadyQueuedCount);

        var settings = await context.LicenseManagementSettings.SingleAsync();
        Assert.NotNull(settings.LastRenewalReminderRunAt);
        Assert.Equal("Success", settings.LastRenewalReminderStatus);
        Assert.Equal(1, settings.LastRenewalReminderDueCount);
        Assert.Equal(0, settings.LastRenewalReminderQueuedCount);

        var rows = await context.NotificationOutboxItems.OrderBy(x => x.Recipient).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(NotificationModuleKeys.LicenseManagement, row.RelatedModule);
            Assert.Equal(NotificationEventKeys.LicenseRenewalDue, row.RelatedEvent);
            Assert.Equal(package.Id.ToString(), row.RelatedEntityId);
            Assert.Equal(NotificationOutboxStatuses.Pending, row.Status);
            Assert.NotNull(row.CorrelationId);
            Assert.Contains("Visual Studio", row.Body);
        });
    }

    [Fact]
    public async Task RunAsync_SkipsFutureCancelledAndAlreadyRenewedPackages()
    {
        await using var context = CreateDbContext();
        await SeedSettingsAndTemplateAsync(context, "owner@example.com", null);
        var future = await SeedDuePackageAsync(context, renewalDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90));
        var cancelled = await SeedDuePackageAsync(context, status: LicensePackageStatus.Cancelled);
        var renewed = await SeedDuePackageAsync(context);
        _ = await SeedDuePackageAsync(context, previousPackageId: renewed.Id, renewalRequired: false);
        var processor = new LicenseRenewalReminderProcessor(context, new NotificationTemplateRenderer());

        var result = await processor.RunAsync();

        Assert.Equal(0, result.DuePackageCount);
        Assert.Empty(await context.NotificationOutboxItems.ToListAsync());
        Assert.NotEqual(Guid.Empty, future.Id);
        Assert.NotEqual(Guid.Empty, cancelled.Id);
    }

    [Fact]
    public async Task SettingsUpdate_RejectsInvalidRenewalRecipient()
    {
        await using var context = CreateDbContext();
        var service = new LicenseManagementSettingsService(context);

        var result = await service.UpdateSettingsAsync(
            new UpdateLicenseManagementSettingsRequest(
                "TRY",
                false,
                30,
                "valid@example.com; not-an-email",
                null,
                null,
                null,
                "tester",
                null,
                null));

        Assert.False(result.IsSuccess);
        Assert.Contains("not-an-email", result.Message);
    }

    private static async Task SeedSettingsAndTemplateAsync(
        AppDbContext context,
        string? recipients,
        string? ccRecipients)
    {
        context.LicenseManagementSettings.Add(new LicenseManagementSettings
        {
            DefaultCurrency = "TRY",
            DefaultRenewalReminderDays = 30,
            DefaultRenewalRecipients = recipients,
            DefaultRenewalCcRecipients = ccRecipients,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        });
        context.NotificationTemplates.Add(new NotificationTemplate
        {
            ModuleKey = NotificationModuleKeys.LicenseManagement,
            EventKey = NotificationEventKeys.LicenseRenewalDue,
            Channel = NotificationChannels.Email,
            Name = "Renewal",
            IsEnabled = true,
            SubjectTemplate = "Renew {{productName}}",
            BodyTemplate = "{{productName}} renews on {{renewalDate}} in {{daysRemaining}} days.",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        });
        await context.SaveChangesAsync();
    }

    private static async Task<LicensePackage> SeedDuePackageAsync(
        AppDbContext context,
        DateOnly? renewalDate = null,
        LicensePackageStatus status = LicensePackageStatus.Active,
        Guid? previousPackageId = null,
        bool renewalRequired = true)
    {
        var product = new LicensedProduct
        {
            Name = "Visual Studio",
            CategoryId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        var purchase = new LicensePurchase
        {
            Title = "Developer tools 2026",
            PurchaseType = LicensePurchaseType.DirectPurchase,
            Status = LicensePurchaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        var package = new LicensePackage
        {
            Product = product,
            ProductId = product.Id,
            Purchase = purchase,
            PurchaseId = purchase.Id,
            LicenseType = LicenseType.NamedUser,
            Quantity = 10,
            RenewalRequired = renewalRequired,
            RenewalDate = renewalDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            Status = status,
            IsActive = status == LicensePackageStatus.Active,
            PreviousPackageId = previousPackageId,
            CreatedAt = DateTime.UtcNow,
        };
        context.LicensePackages.Add(package);
        await context.SaveChangesAsync();
        return package;
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
