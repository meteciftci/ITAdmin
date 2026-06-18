using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Application.Common.Models.Notifications;
using SasPortal.Application.Notifications;
using SasPortal.Domain.Entities;
using SasPortal.Persistence.Context;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdManagementNotificationEnqueueServiceTests
{
    [Fact]
    public async Task EnqueueUserCreated_DisabledSettings_DoesNotQueue()
    {
        await using var dbContext = CreateDbContext();
        await SeedDisabledNotificationSettingsAsync(dbContext);

        var outbox = new FakeNotificationOutboxService();
        var service = CreateService(dbContext, outbox);

        var summary = await service.EnqueueUserCreatedAsync(BuildRequest(phone: "5551234567"), CancellationToken.None);

        Assert.Equal(0, summary.QueuedCount);
        Assert.Empty(outbox.Requests);
    }

    [Fact]
    public async Task EnqueueUserCreated_SmsEnabledWithTemplateAndRecipient_QueuesOutbox()
    {
        await using var dbContext = CreateDbContext();
        await SeedSmsNotificationSettingsAsync(
            dbContext,
            mappingId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await SeedSmsTemplateAsync(dbContext);

        var outbox = new FakeNotificationOutboxService();
        var service = CreateService(dbContext, outbox);

        var summary = await service.EnqueueUserCreatedAsync(
            BuildRequest(
                phone: "5551234567",
                mappingId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            CancellationToken.None);

        Assert.Equal(1, summary.QueuedCount);
        Assert.Single(outbox.Requests);
        Assert.DoesNotContain("password", outbox.Requests[0].Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnqueueUserCreated_SmsEnabledWithoutTemplate_SkipsQueue()
    {
        await using var dbContext = CreateDbContext();
        await SeedSmsNotificationSettingsAsync(
            dbContext,
            mappingId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var outbox = new FakeNotificationOutboxService();
        var service = CreateService(dbContext, outbox);

        var summary = await service.EnqueueUserCreatedAsync(
            BuildRequest(
                phone: "5551234567",
                mappingId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            CancellationToken.None);

        Assert.Equal(0, summary.QueuedCount);
        Assert.Equal(1, summary.SkippedCount);
        Assert.Empty(outbox.Requests);
    }

    [Fact]
    public async Task EnqueueUserCreated_EmailUpnSource_QueuesOutbox()
    {
        await using var dbContext = CreateDbContext();
        await SeedEmailNotificationSettingsAsync(dbContext);
        await SeedEmailTemplateAsync(dbContext);

        var outbox = new FakeNotificationOutboxService();
        var service = CreateService(dbContext, outbox);

        var summary = await service.EnqueueUserCreatedAsync(BuildRequest(), CancellationToken.None);

        Assert.Equal(1, summary.QueuedCount);
        Assert.Equal(NotificationChannels.Email, outbox.Requests[0].Channel);
    }

    private static AdManagementNotificationEnqueueService CreateService(
        AppDbContext dbContext,
        INotificationOutboxService outbox) =>
        new(
            dbContext,
            outbox,
            new NotificationTemplateRenderer(),
            NullLogger<AdManagementNotificationEnqueueService>.Instance);

    private static AdUserCreatedNotificationEnqueueRequest BuildRequest(
        string? phone = null,
        string? mappingId = null)
    {
        var mappings = new List<AdAttributeMappingItem>
        {
            new(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                "mobilePhone",
                "Mobile",
                "mobile",
                true,
                true,
                false,
                false,
                "Phone",
                "None",
                1),
        };

        var mappedAttributes = new List<CreateAdUserMappedAttributeRequest>();
        if (!string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(mappingId))
        {
            mappedAttributes.Add(new CreateAdUserMappedAttributeRequest("mobilePhone", phone));
        }

        var createRequest = new CreateAdUserRequest(
            "Ada",
            "Lovelace",
            "IT",
            null,
            "example.com",
            "OU=Users,DC=example,DC=com",
            "Secret!123",
            true,
            false,
            mappedAttributes,
            Guid.NewGuid(),
            "admin",
            null,
            null);

        var createdUser = new CreateAdUserResponse(
            Guid.NewGuid().ToString(),
            "CN=Ada Lovelace,OU=Users,DC=example,DC=com",
            "Ada Lovelace",
            "alovelace",
            "alovelace@example.com",
            "Ada Lovelace",
            true,
            "ok",
            false,
            null);

        return new AdUserCreatedNotificationEnqueueRequest(createRequest, createdUser, mappings, "admin");
    }

    private static async Task SeedDisabledNotificationSettingsAsync(AppDbContext dbContext)
    {
        var settings = new AdManagementSettings
        {
            IsEnabled = true,
            NotificationSettingsJson = AdManagementNotificationSettingsSerializer.Serialize(
                AdManagementNotificationSettingsSerializer.CreateDefault()),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };

        await dbContext.AdManagementSettings.AddAsync(settings);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSmsNotificationSettingsAsync(AppDbContext dbContext, string mappingId)
    {
        var notificationSettings = new AdManagementNotificationSettings
        {
            Rules =
            [
                new AdManagementNotificationRule
                {
                    Id = Guid.NewGuid(),
                    EventKey = AdManagementNotificationEventKeys.UserCreated,
                    Channel = NotificationChannels.Sms,
                    IsEnabled = true,
                    RecipientSource = new AdManagementNotificationRecipientSource
                    {
                        Type = AdManagementNotificationRecipientSourceTypes.MappedAttribute,
                        Value = mappingId,
                    },
                },
            ],
        };

        var settings = new AdManagementSettings
        {
            IsEnabled = true,
            NotificationSettingsJson = AdManagementNotificationSettingsSerializer.Serialize(notificationSettings),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };

        await dbContext.AdManagementSettings.AddAsync(settings);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedEmailNotificationSettingsAsync(AppDbContext dbContext)
    {
        var notificationSettings = new AdManagementNotificationSettings
        {
            Rules =
            [
                new AdManagementNotificationRule
                {
                    Id = Guid.NewGuid(),
                    EventKey = AdManagementNotificationEventKeys.UserCreated,
                    Channel = NotificationChannels.Email,
                    IsEnabled = true,
                    RecipientSource = new AdManagementNotificationRecipientSource
                    {
                        Type = AdManagementNotificationRecipientSourceTypes.UserPrincipalName,
                    },
                },
            ],
        };

        var settings = new AdManagementSettings
        {
            IsEnabled = true,
            NotificationSettingsJson = AdManagementNotificationSettingsSerializer.Serialize(notificationSettings),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };

        await dbContext.AdManagementSettings.AddAsync(settings);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSmsTemplateAsync(AppDbContext dbContext)
    {
        await dbContext.NotificationTemplates.AddAsync(
            new NotificationTemplate
            {
                ModuleKey = NotificationModuleKeys.AdManagement,
                EventKey = NotificationEventKeys.UserCreated,
                Channel = NotificationChannels.Sms,
                Name = "User created SMS",
                IsEnabled = true,
                BodyTemplate = "Hello {{displayName}}",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test",
            });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedEmailTemplateAsync(AppDbContext dbContext)
    {
        await dbContext.NotificationTemplates.AddAsync(
            new NotificationTemplate
            {
                ModuleKey = NotificationModuleKeys.AdManagement,
                EventKey = NotificationEventKeys.UserCreated,
                Channel = NotificationChannels.Email,
                Name = "User created Email",
                IsEnabled = true,
                SubjectTemplate = "Welcome",
                BodyTemplate = "Hello {{upn}}",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "test",
            });
        await dbContext.SaveChangesAsync();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeNotificationOutboxService : INotificationOutboxService
    {
        public List<NotificationOutboxEnqueueRequest> Requests { get; } = [];

        public Task<NotificationOutboxEnqueueResult> EnqueueAsync(
            NotificationOutboxEnqueueRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new NotificationOutboxEnqueueResult(true, "queued", Guid.NewGuid()));
        }

        public Task<PagedResult<NotificationOutboxListItem>> GetListAsync(
            NotificationOutboxListQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<NotificationOutboxDetail?> GetDetailAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<NotificationOutboxOperationResult> RetryAsync(
            Guid id,
            NotificationOutboxActorRequest actor,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<NotificationOutboxOperationResult> CancelAsync(
            Guid id,
            NotificationOutboxActorRequest actor,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
