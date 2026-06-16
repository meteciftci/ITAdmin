using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed class AdDeletedObjectRestoreReadinessService(
    IAdManagementSettingsService settingsService,
    IAdwsPortConnectivityChecker adwsPortConnectivityChecker,
    IAdDeletedObjectRestoreReadinessPowerShellProbe powerShellProbe,
    IAdOperationLogService operationLogService,
    ILogger<AdDeletedObjectRestoreReadinessService> logger) : IAdDeletedObjectRestoreReadinessService
{
    private const int AdwsPort = 9389;
    private const int ReadinessTimeoutCapSeconds = 15;
    private const int PowerShellTimeoutWarningThresholdSeconds = 10;
    private const int PowerShellTimeoutMinSeconds = 5;
    private const int PowerShellTimeoutMaxSeconds = 300;

    public async Task<AdDeletedObjectRestoreReadinessResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var checks = new List<AdDeletedObjectRestoreReadinessCheck>();

        try
        {
            var settings = await settingsService.GetSettingsAsync(cancellationToken);
            if (!settings.IsConfigured)
            {
                checks.Add(CreateSettingsCheck(
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.SettingsIncompleteMessage,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.SettingsIncompleteRemediation));
                return BuildResult(
                    checks,
                    checkedAt,
                    null,
                    AdDeletedObjectRestoreReadinessI18nKeys.Summary.SettingsIncomplete);
            }

            if (!settings.IsEnabled)
            {
                checks.Add(CreateSettingsCheck(
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.ModuleDisabledMessage,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.ModuleDisabledRemediation));
                return BuildResult(
                    checks,
                    checkedAt,
                    null,
                    AdDeletedObjectRestoreReadinessI18nKeys.Summary.ModuleDisabled);
            }

            var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);
            if (connection is null
                || string.IsNullOrWhiteSpace(connection.DomainFqdn)
                || string.IsNullOrWhiteSpace(connection.DefaultNamingContext))
            {
                checks.Add(CreateSettingsCheck(
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.ConnectionIncompleteMessage,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.ConnectionIncompleteRemediation));
                return BuildResult(
                    checks,
                    checkedAt,
                    null,
                    AdDeletedObjectRestoreReadinessI18nKeys.Summary.SettingsIncomplete);
            }

            var domainController = ResolvePrimaryHost(connection);
            var timeout = ResolveReadinessTimeout(settings.PowerShellTimeoutSeconds);
            var probeRequest = new AdDeletedObjectRestoreReadinessPowerShellProbeRequest(
                domainController,
                connection.DomainFqdn,
                connection.ServiceAccountUserName,
                connection.ServiceAccountPassword,
                connection.NetbiosDomainName,
                timeout);

            checks.Add(await CheckPowerShellTimeoutAsync(settings.PowerShellTimeoutSeconds, cancellationToken));

            var moduleCheck = await CheckActiveDirectoryModuleAsync(probeRequest, cancellationToken);
            checks.Add(moduleCheck);

            var moduleAvailable = moduleCheck.Status == AdDeletedObjectRestoreReadinessCheckStatuses.Success;

            if (moduleAvailable)
            {
                checks.Add(await CheckRestoreAdObjectCommandAsync(probeRequest, cancellationToken));
                checks.Add(await CheckRecycleBinFeatureAsync(probeRequest, domainController, cancellationToken));
                checks.Add(await CheckServiceAccountAdwsReadAsync(
                    probeRequest,
                    domainController,
                    cancellationToken));
            }
            else
            {
                checks.Add(CreateNotCheckedCheck(
                    AdDeletedObjectRestoreReadinessCheckKeys.RestoreAdObjectCommand,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestoreAdObjectCommand.Title,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestoreAdObjectCommand.NotChecked));
                checks.Add(CreateNotCheckedCheck(
                    AdDeletedObjectRestoreReadinessCheckKeys.RecycleBinFeature,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.Title,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.VerificationFailed));
                checks.Add(CreateNotCheckedCheck(
                    AdDeletedObjectRestoreReadinessCheckKeys.ServiceAccountAdwsRead,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.Title,
                    AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.NotChecked));
            }

            checks.Add(await CheckAdwsPortConnectivityAsync(domainController, timeout, cancellationToken));
            checks.Add(await CheckRestorePermissionViaOperationLogAsync(cancellationToken));

            return BuildResult(checks, checkedAt, domainController);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AD deleted object restore readiness check failed unexpectedly.");

            checks.Add(CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.AdManagementSettings,
                AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.Title,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.UnexpectedFailureMessage,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.UnexpectedFailureRemediation,
                null,
                true,
                null));

            return BuildResult(
                checks,
                checkedAt,
                null,
                AdDeletedObjectRestoreReadinessI18nKeys.Summary.UnexpectedFailure);
        }
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckPowerShellTimeoutAsync(
        int configuredTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (configuredTimeoutSeconds < PowerShellTimeoutMinSeconds
            || configuredTimeoutSeconds > PowerShellTimeoutMaxSeconds)
        {
            return CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.PowerShellTimeout,
                AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.Title,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.Failed,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.RemediationInvalidRange,
                null,
                true,
                null,
                CreateParams(
                    ("minSeconds", PowerShellTimeoutMinSeconds),
                    ("maxSeconds", PowerShellTimeoutMaxSeconds)));
        }

        if (configuredTimeoutSeconds < PowerShellTimeoutWarningThresholdSeconds)
        {
            return CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.PowerShellTimeout,
                AdDeletedObjectRestoreReadinessCheckStatuses.Warning,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.Title,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.Warning,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.RemediationRecommendHigher,
                null,
                false,
                null,
                CreateParams(
                    ("configuredTimeoutSeconds", configuredTimeoutSeconds),
                    ("timeoutWarningThresholdSeconds", PowerShellTimeoutWarningThresholdSeconds)));
        }

        return CreateCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.PowerShellTimeout,
            AdDeletedObjectRestoreReadinessCheckStatuses.Success,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.Title,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.PowerShellTimeout.Success,
            null,
            null,
            false,
            null,
            CreateParams(("configuredTimeoutSeconds", configuredTimeoutSeconds)));
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckActiveDirectoryModuleAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await powerShellProbe.CheckActiveDirectoryModuleAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            return CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.ActiveDirectoryPowerShellModule,
                AdDeletedObjectRestoreReadinessCheckStatuses.Success,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.ActiveDirectoryPowerShellModule.Title,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.ActiveDirectoryPowerShellModule.Success,
                null,
                null,
                false,
                null);
        }

        return CreateCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.ActiveDirectoryPowerShellModule,
            AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.ActiveDirectoryPowerShellModule.Title,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.ActiveDirectoryPowerShellModule.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.ActiveDirectoryPowerShellModule.Remediation,
            AdDeletedObjectRestoreReadinessCommandBuilder.BuildInstallRsatCommand(),
            true,
            result.ErrorSummary);
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckRestoreAdObjectCommandAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await powerShellProbe.CheckRestoreAdObjectCommandAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            return CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.RestoreAdObjectCommand,
                AdDeletedObjectRestoreReadinessCheckStatuses.Success,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestoreAdObjectCommand.Title,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestoreAdObjectCommand.Success,
                null,
                null,
                false,
                null);
        }

        return CreateCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.RestoreAdObjectCommand,
            AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestoreAdObjectCommand.Title,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestoreAdObjectCommand.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestoreAdObjectCommand.Remediation,
            "Get-Command Restore-ADObject",
            true,
            result.ErrorSummary);
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckAdwsPortConnectivityAsync(
        string domainController,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var sanitizedHost = AdDeletedObjectRestoreReadinessCommandBuilder.SanitizeHost(domainController);
        var connected = await adwsPortConnectivityChecker.CanConnectAsync(
            sanitizedHost,
            AdwsPort,
            timeout,
            cancellationToken);

        var hostPortParams = CreateParams(
            ("host", sanitizedHost),
            ("port", AdwsPort));

        if (connected)
        {
            return CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.AdwsPortConnectivity,
                AdDeletedObjectRestoreReadinessCheckStatuses.Success,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdwsPortConnectivity.Title,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdwsPortConnectivity.Success,
                null,
                null,
                false,
                null,
                hostPortParams);
        }

        return CreateCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.AdwsPortConnectivity,
            AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdwsPortConnectivity.Title,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdwsPortConnectivity.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdwsPortConnectivity.Remediation,
            AdDeletedObjectRestoreReadinessCommandBuilder.BuildTestNetConnectionCommand(sanitizedHost),
            true,
            null,
            hostPortParams);
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckRecycleBinFeatureAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        string domainController,
        CancellationToken cancellationToken)
    {
        var result = await powerShellProbe.CheckRecycleBinFeatureAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            return CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.RecycleBinFeature,
                AdDeletedObjectRestoreReadinessCheckStatuses.Success,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.Title,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.Success,
                null,
                null,
                false,
                null);
        }

        var messageKey = ContainsToken(result.ErrorSummary, AdDeletedObjectRestoreReadinessPowerShellProbe.RecycleBinDisabledErrorToken)
            ? AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.Disabled
            : AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.VerificationFailed;

        return CreateCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.RecycleBinFeature,
            AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.Title,
            messageKey,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RecycleBinFeature.Remediation,
            AdDeletedObjectRestoreReadinessCommandBuilder.BuildEnableRecycleBinCommand(
                request.DomainFqdn,
                domainController),
            true,
            result.ErrorSummary);
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckServiceAccountAdwsReadAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        string domainController,
        CancellationToken cancellationToken)
    {
        var result = await powerShellProbe.CheckAdwsReadAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            var usesServiceAccount = !string.IsNullOrWhiteSpace(request.ServiceAccountUserName)
                && !string.IsNullOrWhiteSpace(request.ServiceAccountPassword);

            return CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.ServiceAccountAdwsRead,
                AdDeletedObjectRestoreReadinessCheckStatuses.Success,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.Title,
                usesServiceAccount
                    ? AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.SuccessServiceAccount
                    : AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.SuccessProcessIdentity,
                null,
                null,
                false,
                null);
        }

        return CreateCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.ServiceAccountAdwsRead,
            AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.Title,
            ResolveAdwsReadFailureMessageKey(result.ErrorSummary),
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.Remediation,
            $"Get-ADRootDSE -Server {AdDeletedObjectRestoreReadinessCommandBuilder.SanitizeHost(domainController)}",
            true,
            result.ErrorSummary);
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckRestorePermissionViaOperationLogAsync(
        CancellationToken cancellationToken)
    {
        var logs = await operationLogService.GetLogsAsync(
            new AdOperationLogListQuery(
                AdManagementOperationTypes.DeletedObjectRestore,
                AdManagementOperationStatuses.Succeeded,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                PageNumber: 1,
                PageSize: 1),
            cancellationToken);

        if (logs.Items.Count > 0)
        {
            return CreateCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.RestorePermissionNotVerified,
                AdDeletedObjectRestoreReadinessCheckStatuses.Success,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestorePermissionVerification.Title,
                AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestorePermissionVerification.Verified,
                null,
                null,
                false,
                null);
        }

        return CreateCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.RestorePermissionNotVerified,
            AdDeletedObjectRestoreReadinessCheckStatuses.NotChecked,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestorePermissionVerification.Title,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestorePermissionVerification.NotVerified,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.RestorePermissionVerification.Remediation,
            null,
            false,
            null);
    }

    private static AdDeletedObjectRestoreReadinessCheck CreateSettingsCheck(
        string messageKey,
        string remediationKey) =>
        CreateCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.AdManagementSettings,
            AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
            AdDeletedObjectRestoreReadinessI18nKeys.Checks.AdManagementSettings.Title,
            messageKey,
            remediationKey,
            null,
            true,
            null);

    private static AdDeletedObjectRestoreReadinessCheck CreateNotCheckedCheck(
        string key,
        string titleKey,
        string messageKey) =>
        CreateCheck(
            key,
            AdDeletedObjectRestoreReadinessCheckStatuses.NotChecked,
            titleKey,
            messageKey,
            null,
            null,
            false,
            null);

    private static AdDeletedObjectRestoreReadinessCheck CreateCheck(
        string key,
        string status,
        string titleKey,
        string? messageKey,
        string? remediationKey,
        string? command,
        bool isBlocking,
        string? details,
        IReadOnlyDictionary<string, object>? messageParams = null) =>
        new(
            key,
            status,
            titleKey,
            messageKey,
            remediationKey,
            command,
            isBlocking,
            details,
            titleKey,
            null,
            messageKey,
            messageParams,
            remediationKey,
            null);

    private static AdDeletedObjectRestoreReadinessResult BuildResult(
        IReadOnlyList<AdDeletedObjectRestoreReadinessCheck> checks,
        DateTimeOffset checkedAt,
        string? domainController,
        string? summaryKeyOverride = null,
        IReadOnlyDictionary<string, object>? summaryParams = null)
    {
        var blockingReasons = checks
            .Where(check => check.IsBlocking && check.Status == AdDeletedObjectRestoreReadinessCheckStatuses.Failed)
            .ToList();
        var warnings = checks
            .Where(check => check.Status == AdDeletedObjectRestoreReadinessCheckStatuses.Warning)
            .ToList();

        var isReady = blockingReasons.Count == 0;
        var status = !isReady
            ? AdDeletedObjectRestoreReadinessStatuses.NotReady
            : warnings.Count > 0
                ? AdDeletedObjectRestoreReadinessStatuses.Warning
                : AdDeletedObjectRestoreReadinessStatuses.Ready;

        var summaryKey = summaryKeyOverride ?? status switch
        {
            AdDeletedObjectRestoreReadinessStatuses.Ready => AdDeletedObjectRestoreReadinessI18nKeys.Summary.Ready,
            AdDeletedObjectRestoreReadinessStatuses.Warning => AdDeletedObjectRestoreReadinessI18nKeys.Summary.Warning,
            _ => AdDeletedObjectRestoreReadinessI18nKeys.Summary.NotReady,
        };

        return new AdDeletedObjectRestoreReadinessResult(
            isReady,
            status,
            summaryKey,
            blockingReasons,
            warnings,
            checks,
            checkedAt,
            domainController,
            summaryKey,
            summaryParams);
    }

    private static IReadOnlyDictionary<string, object> CreateParams(
        params (string Key, object Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Value);

    private static TimeSpan ResolveReadinessTimeout(int configuredTimeoutSeconds)
    {
        var cappedSeconds = Math.Min(
            Math.Max(configuredTimeoutSeconds, PowerShellTimeoutMinSeconds),
            ReadinessTimeoutCapSeconds);
        return TimeSpan.FromSeconds(cappedSeconds);
    }

    private static string ResolvePrimaryHost(AdManagementConnectionParameters connection)
    {
        if (connection.PreferredDomainControllers.Count > 0
            && !string.IsNullOrWhiteSpace(connection.PreferredDomainControllers[0]))
        {
            return connection.PreferredDomainControllers[0];
        }

        return connection.DomainFqdn ?? string.Empty;
    }

    private static string ResolveAdwsReadFailureMessageKey(string? errorSummary)
    {
        if (string.IsNullOrWhiteSpace(errorSummary))
        {
            return AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.Failed;
        }

        var lower = errorSummary.ToLowerInvariant();
        if (lower.Contains("access is denied", StringComparison.Ordinal)
            || lower.Contains("access denied", StringComparison.Ordinal)
            || lower.Contains("unauthorized", StringComparison.Ordinal)
            || lower.Contains("insufficient", StringComparison.Ordinal))
        {
            return AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.FailedAccessDenied;
        }

        if (lower.Contains("timed out", StringComparison.Ordinal)
            || lower.Contains("timeout", StringComparison.Ordinal))
        {
            return AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.FailedTimeout;
        }

        if (lower.Contains("unavailable", StringComparison.Ordinal)
            || lower.Contains("cannot connect", StringComparison.Ordinal)
            || lower.Contains("could not connect", StringComparison.Ordinal)
            || lower.Contains("server not operational", StringComparison.Ordinal))
        {
            return AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.FailedConnection;
        }

        return AdDeletedObjectRestoreReadinessI18nKeys.Checks.ServiceAccountAdwsRead.Failed;
    }

    private static bool ContainsToken(string? value, string token) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(token, StringComparison.Ordinal);
}
