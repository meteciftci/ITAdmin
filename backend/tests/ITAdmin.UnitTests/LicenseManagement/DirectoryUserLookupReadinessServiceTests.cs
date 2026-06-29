using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Models;
using ITAdmin.Infrastructure.Services;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class DirectoryUserLookupReadinessServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenModuleDisabled_ReturnsNotReadyWithoutSettingsDetails()
    {
        var settings = BuildReadySettings() with { IsEnabled = false };
        var service = new DirectoryUserLookupReadinessService(
            new FakeAdManagementSettingsService(settings, "secret"));

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        Assert.Equal("ModuleDisabled", result.Reason);
        Assert.DoesNotContain("baseDn", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenConfiguredAndValidated_ReturnsReady()
    {
        var service = new DirectoryUserLookupReadinessService(
            new FakeAdManagementSettingsService(BuildReadySettings(), "secret"));

        var result = await service.CheckAsync();

        Assert.True(result.IsReady);
        Assert.Equal("Ready", result.Reason);
        Assert.Null(result.Message);
    }

    private static AdManagementSettingsModel BuildReadySettings() =>
        new(
            true,
            true,
            "example.com",
            null,
            null,
            null,
            null,
            "EXAMPLE",
            "DC=example,DC=com",
            "DC=example,DC=com",
            null,
            null,
            null,
            null,
            [],
            "svc-account",
            true,
            false,
            30,
            DateTime.UtcNow,
            "Ok",
            null,
            new AdManagementNotificationSettings());

    private sealed class FakeAdManagementSettingsService(
        AdManagementSettingsModel settings,
        string? password) : IAdManagementSettingsService
    {
        public Task<AdManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task<UpdateAdManagementSettingsResult> UpdateSettingsAsync(
            UpdateAdManagementSettingsRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AdManagementConnectionParameters?> GetConnectionParametersAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AdManagementConnectionParameters?>(
                new AdManagementConnectionParameters(
                    settings.DomainFqdn,
                    settings.NetbiosDomainName,
                    settings.DefaultNamingContext,
                    settings.BaseDn,
                    settings.UsersRootOu,
                    settings.DisabledUsersOu,
                    settings.GroupsSearchBase,
                    settings.ComputersSearchBase,
                    settings.PreferredDomainControllers,
                    settings.ServiceAccountUserName,
                    password));

        public Task RecordValidationResultAsync(
            AdManagementValidationResult result,
            AdManagementValidationRequest request,
            string? primaryDomainController,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
