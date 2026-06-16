using System.Reflection;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Infrastructure.Services;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdDeletedObjectRestoreReadinessServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenAllChecksPass_ReturnsReady()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = SuccessProbe(),
            },
            new FakeAdwsPortConnectivityChecker(true));

        var result = await service.CheckAsync();

        Assert.True(result.IsReady);
        Assert.Equal(AdDeletedObjectRestoreReadinessStatuses.Ready, result.Status);
        Assert.Empty(result.BlockingReasons);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task CheckAsync_WhenActiveDirectoryModuleMissing_ReturnsNotReady()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = FailedProbe(AdDeletedObjectRestoreReadinessPowerShellProbe.ModuleMissingErrorToken),
            },
            new FakeAdwsPortConnectivityChecker(true));

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        Assert.Equal(AdDeletedObjectRestoreReadinessStatuses.NotReady, result.Status);
        Assert.Contains(
            result.BlockingReasons,
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.ActiveDirectoryPowerShellModule);
    }

    [Fact]
    public async Task CheckAsync_WhenRestoreAdObjectCommandMissing_ReturnsNotReady()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = FailedProbe(
                    AdDeletedObjectRestoreReadinessPowerShellProbe.RestoreCommandMissingErrorToken),
            },
            new FakeAdwsPortConnectivityChecker(true));

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        Assert.Contains(
            result.BlockingReasons,
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.RestoreAdObjectCommand);
    }

    [Fact]
    public async Task CheckAsync_WhenAdwsPortBlocked_ReturnsNotReady()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = SuccessProbe(),
            },
            new FakeAdwsPortConnectivityChecker(false));

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        var adwsCheck = result.BlockingReasons.Single(
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.AdwsPortConnectivity);
        Assert.Contains("Test-NetConnection", adwsCheck.Command ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("9389", adwsCheck.Command ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_WhenRecycleBinDisabled_ReturnsNotReady()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = FailedProbe(
                    AdDeletedObjectRestoreReadinessPowerShellProbe.RecycleBinDisabledErrorToken),
            },
            new FakeAdwsPortConnectivityChecker(true));

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        var recycleBinCheck = result.BlockingReasons.Single(
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.RecycleBinFeature);
        Assert.Contains(
            "Enable-ADOptionalFeature",
            recycleBinCheck.Command ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_WhenServiceAccountAdwsReadFails_ReturnsNotReady()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = FailedProbe("Access is denied"),
            },
            new FakeAdwsPortConnectivityChecker(true));

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        Assert.Contains(
            result.BlockingReasons,
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.ServiceAccountAdwsRead);
    }

    [Fact]
    public async Task CheckAsync_WhenRestorePermissionOperationLogMissing_ReturnsNotChecked()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = SuccessProbe(),
            },
            new FakeAdwsPortConnectivityChecker(true));

        var result = await service.CheckAsync();

        Assert.DoesNotContain(
            result.Warnings,
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.RestorePermissionNotVerified);
        Assert.Contains(
            result.Checks,
            check =>
                check.Key == AdDeletedObjectRestoreReadinessCheckKeys.RestorePermissionNotVerified
                && check.Status == AdDeletedObjectRestoreReadinessCheckStatuses.NotChecked);
        Assert.Equal(AdDeletedObjectRestoreReadinessStatuses.Ready, result.Status);
        Assert.True(result.IsReady);
    }

    [Fact]
    public async Task CheckAsync_WhenSuccessfulRestoreOperationLogExists_RestoresPermissionCheckIsSuccess()
    {
        var operationLogService = new FakeAdOperationLogService(hasSucceededRestoreLog: true);
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = SuccessProbe(),
            },
            new FakeAdwsPortConnectivityChecker(true),
            operationLogService: operationLogService);

        var result = await service.CheckAsync();

        Assert.Equal(AdDeletedObjectRestoreReadinessStatuses.Ready, result.Status);
        Assert.True(result.IsReady);
        Assert.Empty(result.BlockingReasons);
        Assert.Empty(result.Warnings);

        Assert.Contains(
            result.Checks,
            check =>
                check.Key == AdDeletedObjectRestoreReadinessCheckKeys.RestorePermissionNotVerified
                && check.Status == AdDeletedObjectRestoreReadinessCheckStatuses.Success
                && check.MessageKey
                    == AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestorePermissionVerification.Verified);

        Assert.Equal(1, operationLogService.GetLogsAsyncCallCount);
        Assert.Equal(
            AdManagementOperationTypes.DeletedObjectRestore,
            operationLogService.LastQuery?.OperationType);
        Assert.Equal(
            AdManagementOperationStatuses.Succeeded,
            operationLogService.LastQuery?.Status);
        Assert.Equal(1, operationLogService.LastQuery?.PageNumber);
        Assert.Equal(1, operationLogService.LastQuery?.PageSize);
    }

    [Fact]
    public async Task CheckAsync_WhenSettingsNotConfigured_ReturnsNotReady()
    {
        var service = CreateService(
            new FakePowerShellProbe(),
            new FakeAdwsPortConnectivityChecker(true),
            settings: CreateSettings(isConfigured: false));

        var result = await service.CheckAsync();

        Assert.False(result.IsReady);
        Assert.Contains(
            result.BlockingReasons,
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.AdManagementSettings);
    }

    [Fact]
    public async Task CheckAsync_ResponseDoesNotContainPasswordOrCredential()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = SuccessProbe(),
            },
            new FakeAdwsPortConnectivityChecker(true),
            connectionPassword: "SuperSecretPassword123!");

        var result = await service.CheckAsync();
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.DoesNotContain("SuperSecretPassword123!", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadinessService_DoesNotContainRawTurkishUserFacingStrings()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdDeletedObjectRestoreReadinessService.cs"));

        Assert.DoesNotContain("Geri yükleme", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bağlantısı başarılı", source, StringComparison.Ordinal);
        Assert.DoesNotContain("değeri uygun", source, StringComparison.Ordinal);
        Assert.DoesNotContain("doğrulandı", source, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestoreReadinessI18nKeys", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_WhenPowerShellTimeoutConfigured_ReturnsKeyWithSecondsParam()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = SuccessProbe(),
            },
            new FakeAdwsPortConnectivityChecker(true),
            settings: CreateSettings(powerShellTimeoutSeconds: 30));

        var result = await service.CheckAsync();

        var timeoutCheck = result.Checks.Single(
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.PowerShellTimeout);
        Assert.Equal(
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.Success,
            timeoutCheck.MessageKey);
        Assert.NotNull(timeoutCheck.MessageParams);
        Assert.Equal(30, timeoutCheck.MessageParams!["configuredTimeoutSeconds"]);
    }

    [Fact]
    public async Task CheckAsync_WhenAdwsPortBlocked_ReturnsHostPortParams()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = SuccessProbe(),
            },
            new FakeAdwsPortConnectivityChecker(false));

        var result = await service.CheckAsync();

        var adwsCheck = result.BlockingReasons.Single(
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.AdwsPortConnectivity);
        Assert.Equal(
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdwsPortConnectivity.Failed,
            adwsCheck.MessageKey);
        Assert.NotNull(adwsCheck.MessageParams);
        Assert.Equal("dc1.corp.example.com", adwsCheck.MessageParams!["host"]);
        Assert.Equal(9389, adwsCheck.MessageParams!["port"]);
    }

    [Fact]
    public async Task CheckAsync_WhenRecycleBinDisabled_ReturnsDisabledMessageKey()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = FailedProbe(
                    AdDeletedObjectRestoreReadinessPowerShellProbe.RecycleBinDisabledErrorToken),
            },
            new FakeAdwsPortConnectivityChecker(true));

        var result = await service.CheckAsync();

        var recycleBinCheck = result.BlockingReasons.Single(
            check => check.Key == AdDeletedObjectRestoreReadinessCheckKeys.RecycleBinFeature);
        Assert.Equal(
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.Disabled,
            recycleBinCheck.MessageKey);
        Assert.Equal(
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.Remediation,
            recycleBinCheck.RemediationKey);
    }

    [Fact]
    public async Task CheckAsync_WhenAllChecksPass_ReturnsReadySummaryKey()
    {
        var service = CreateService(
            new FakePowerShellProbe
            {
                ModuleResult = SuccessProbe(),
                RestoreCommandResult = SuccessProbe(),
                RecycleBinResult = SuccessProbe(),
                AdwsReadResult = SuccessProbe(),
            },
            new FakeAdwsPortConnectivityChecker(true));

        var result = await service.CheckAsync();

        Assert.Equal(
            AdDeletedObjectRestoreReadinessI18nKeys.Summary.Ready,
            result.SummaryKey);
        Assert.Equal(result.SummaryKey, result.SummaryMessage);
    }

    [Fact]
    public void RestoreReadinessEndpoint_RequiresDeletedObjectsRestorePermission()
    {
        var method = typeof(SasPortal.Api.Controllers.AdManagementController)
            .GetMethod(nameof(SasPortal.Api.Controllers.AdManagementController.GetDeletedObjectRestoreReadiness));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<SasPortal.Api.Authorization.RequirePermissionAttribute>();
        Assert.Equal(
            SasPortal.Api.Authorization.RequirePermissionAttribute.PolicyPrefix
            + AdManagementPermissions.DeletedObjectsRestore,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void SettingsValidationResponse_IncludesOptionalRestoreReadiness()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Contracts/AdManagement/AdManagementValidationResponse.cs"));

        Assert.Contains("RestoreReadiness", source, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestoreReadinessResponse?", source, StringComparison.Ordinal);
    }

    private static AdDeletedObjectRestoreReadinessService CreateService(
        FakePowerShellProbe powerShellProbe,
        FakeAdwsPortConnectivityChecker portChecker,
        AdManagementSettingsModel? settings = null,
        string? connectionPassword = "secret",
        FakeAdOperationLogService? operationLogService = null)
    {
        var effectiveSettings = settings ?? CreateSettings();
        var opLogService = operationLogService ?? new FakeAdOperationLogService(hasSucceededRestoreLog: false);
        return new AdDeletedObjectRestoreReadinessService(
            new FakeAdManagementSettingsService(effectiveSettings, connectionPassword),
            portChecker,
            powerShellProbe,
            opLogService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AdDeletedObjectRestoreReadinessService>.Instance);
    }

    private sealed class FakeAdOperationLogService(bool hasSucceededRestoreLog) : IAdOperationLogService
    {
        public int GetLogsAsyncCallCount { get; private set; }

        public AdOperationLogListQuery? LastQuery { get; private set; }

        public Task WriteAsync(
            AdOperationLogEntry entry,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PagedResult<AdOperationLogListItem>> GetLogsAsync(
            AdOperationLogListQuery query,
            CancellationToken cancellationToken = default)
        {
            GetLogsAsyncCallCount++;
            LastQuery = query;

            var items = hasSucceededRestoreLog
                ? new List<AdOperationLogListItem>
                {
                    new(
                        Guid.NewGuid(),
                        DateTimeOffset.UtcNow,
                        AdManagementOperationTypes.DeletedObjectRestore,
                        AdManagementOperationStatuses.Succeeded,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        false,
                        false,
                        false,
                        false),
                }
                : new List<AdOperationLogListItem>();

            return Task.FromResult(
                new PagedResult<AdOperationLogListItem>(
                    items,
                    query.PageNumber,
                    query.PageSize,
                    items.Count,
                    1));
        }

        public Task<AdOperationLogDetail?> GetLogByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdOperationLogDetail?>(null);
    }

    private static AdManagementSettingsModel CreateSettings(
        bool isConfigured = true,
        bool isEnabled = true,
        int powerShellTimeoutSeconds = 30) =>
        new(
            isConfigured,
            isEnabled,
            "corp.example.com",
            null,
            "CORP",
            "DC=corp,DC=example,DC=com",
            "DC=corp,DC=example,DC=com",
            "OU=Users,DC=corp,DC=example,DC=com",
            "OU=Disabled,DC=corp,DC=example,DC=com",
            null,
            null,
            ["dc1.corp.example.com"],
            true,
            636,
            "svc_ad_mgmt",
            true,
            false,
            powerShellTimeoutSeconds,
            null,
            null,
            null,
            new Application.Common.AdManagement.AdManagementNotificationSettings());

    private static AdDeletedObjectRestoreReadinessPowerShellProbeResult SuccessProbe() =>
        new(true, null, null);

    private static AdDeletedObjectRestoreReadinessPowerShellProbeResult FailedProbe(string error) =>
        new(false, error, null);

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
                    settings.UseSsl,
                    settings.LdapPort,
                    settings.ServiceAccountUserName,
                    password));

        public Task RecordValidationResultAsync(
            AdManagementValidationResult result,
            AdManagementValidationRequest request,
            string? primaryDomainController,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakePowerShellProbe : IAdDeletedObjectRestoreReadinessPowerShellProbe
    {
        public AdDeletedObjectRestoreReadinessPowerShellProbeResult ModuleResult { get; init; } =
            new(false, AdDeletedObjectRestoreReadinessPowerShellProbe.ModuleMissingErrorToken, null);

        public AdDeletedObjectRestoreReadinessPowerShellProbeResult RestoreCommandResult { get; init; } =
            new(false, AdDeletedObjectRestoreReadinessPowerShellProbe.RestoreCommandMissingErrorToken, null);

        public AdDeletedObjectRestoreReadinessPowerShellProbeResult RecycleBinResult { get; init; } =
            new(false, AdDeletedObjectRestoreReadinessPowerShellProbe.RecycleBinDisabledErrorToken, null);

        public AdDeletedObjectRestoreReadinessPowerShellProbeResult AdwsReadResult { get; init; } =
            new(false, "Access is denied", null);

        public Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckActiveDirectoryModuleAsync(
            AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ModuleResult);

        public Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckRestoreAdObjectCommandAsync(
            AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RestoreCommandResult);

        public Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckRecycleBinFeatureAsync(
            AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RecycleBinResult);

        public Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckAdwsReadAsync(
            AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AdwsReadResult);
    }

    private sealed class FakeAdwsPortConnectivityChecker(bool canConnect) : IAdwsPortConnectivityChecker
    {
        public Task<bool> CanConnectAsync(
            string host,
            int port,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canConnect);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend", "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved.");
    }
}
