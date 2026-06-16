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
    ILogger<AdDeletedObjectRestoreReadinessService> logger) : IAdDeletedObjectRestoreReadinessService
{
    private const int AdwsPort = 9389;
    private const int ReadinessTimeoutCapSeconds = 15;
    private const int PowerShellTimeoutWarningThresholdSeconds = 10;
    private const int PowerShellTimeoutMinSeconds = 5;
    private const int PowerShellTimeoutMaxSeconds = 300;

    private const string SettingsNotReadyMessage =
        "AD yönetim ayarları tamamlanmadan silinen nesne geri yükleme gereksinimleri kontrol edilemez.";
    private const string SettingsDisabledMessage =
        "AD yönetim modülü etkin değil. Silinen nesne geri yükleme kullanılamaz.";
    private const string ReadySummaryMessage = "Silinen nesne geri yükleme gereksinimleri hazır.";
    private const string WarningSummaryMessage = "Silinen nesne geri yükleme gereksinimleri uyarı içeriyor.";
    private const string NotReadySummaryMessage =
        "Silinen nesne geri yükleme özelliği şu anda kullanılamıyor.";

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
                    SettingsNotReadyMessage,
                    "AD yönetim ayarlarını tamamlayın ve kaydedin."));
                return BuildResult(checks, checkedAt, null);
            }

            if (!settings.IsEnabled)
            {
                checks.Add(CreateSettingsCheck(
                    SettingsDisabledMessage,
                    "AD yönetim modülünü etkinleştirin."));
                return BuildResult(checks, checkedAt, null);
            }

            var connection = await settingsService.GetConnectionParametersAsync(cancellationToken);
            if (connection is null
                || string.IsNullOrWhiteSpace(connection.DomainFqdn)
                || string.IsNullOrWhiteSpace(connection.DefaultNamingContext))
            {
                checks.Add(CreateSettingsCheck(
                    SettingsNotReadyMessage,
                    "AD yönetim bağlantı ayarlarını tamamlayın."));
                return BuildResult(checks, checkedAt, null);
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
                    "Restore-ADObject komutu",
                    "Active Directory PowerShell modülü doğrulanamadı."));
                checks.Add(CreateNotCheckedCheck(
                    AdDeletedObjectRestoreReadinessCheckKeys.RecycleBinFeature,
                    "AD Recycle Bin Feature",
                    "Active Directory PowerShell modülü doğrulanamadı."));
                checks.Add(CreateNotCheckedCheck(
                    AdDeletedObjectRestoreReadinessCheckKeys.ServiceAccountAdwsRead,
                    "ADWS temel okuma",
                    "Active Directory PowerShell modülü doğrulanamadı."));
            }

            checks.Add(await CheckAdwsPortConnectivityAsync(domainController, timeout, cancellationToken));
            checks.Add(CreateRestorePermissionWarningCheck());

            return BuildResult(checks, checkedAt, domainController);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AD deleted object restore readiness check failed unexpectedly.");

            checks.Add(new AdDeletedObjectRestoreReadinessCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.AdManagementSettings,
                AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
                "Geri yükleme gereksinimleri",
                "Geri yükleme gereksinimleri doğrulanamadı.",
                "Bir süre sonra tekrar deneyin veya sistem yöneticinize başvurun.",
                null,
                true,
                null));

            return BuildResult(checks, checkedAt, null);
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
            return new AdDeletedObjectRestoreReadinessCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.PowerShellTimeout,
                AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
                "PowerShell zaman aşımı",
                "PowerShellTimeoutSeconds geçerli aralıkta değil.",
                "PowerShell zaman aşımını 5 ile 300 saniye arasında ayarlayın.",
                null,
                true,
                null);
        }

        if (configuredTimeoutSeconds < PowerShellTimeoutWarningThresholdSeconds)
        {
            return new AdDeletedObjectRestoreReadinessCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.PowerShellTimeout,
                AdDeletedObjectRestoreReadinessCheckStatuses.Warning,
                "PowerShell zaman aşımı",
                $"PowerShellTimeoutSeconds değeri düşük ({configuredTimeoutSeconds}s).",
                "Geri yükleme komutları için en az 10 saniye önerilir.",
                null,
                false,
                null);
        }

        return new AdDeletedObjectRestoreReadinessCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.PowerShellTimeout,
            AdDeletedObjectRestoreReadinessCheckStatuses.Success,
            "PowerShell zaman aşımı",
            $"PowerShellTimeoutSeconds değeri uygun ({configuredTimeoutSeconds}s).",
            null,
            null,
            false,
            null);
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckActiveDirectoryModuleAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await powerShellProbe.CheckActiveDirectoryModuleAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            return SuccessCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.ActiveDirectoryPowerShellModule,
                "Active Directory PowerShell modülü",
                "Active Directory PowerShell modülü uygulama sunucusunda bulundu.");
        }

        return FailedCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.ActiveDirectoryPowerShellModule,
            "Active Directory PowerShell modülü",
            "Active Directory PowerShell modülü uygulama sunucusunda bulunamadı.",
            "Uygulama sunucusunda RSAT Active Directory PowerShell modülünü yükleyin.",
            AdDeletedObjectRestoreReadinessCommandBuilder.BuildInstallRsatCommand(),
            result.ErrorSummary);
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckRestoreAdObjectCommandAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await powerShellProbe.CheckRestoreAdObjectCommandAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            return SuccessCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.RestoreAdObjectCommand,
                "Restore-ADObject komutu",
                "Restore-ADObject komutu kullanılabilir.");
        }

        return FailedCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.RestoreAdObjectCommand,
            "Restore-ADObject komutu",
            "Restore-ADObject komutu bulunamadı.",
            "Uygulama sunucusunda RSAT Active Directory PowerShell modülünü yükleyin.",
            "Get-Command Restore-ADObject",
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

        if (connected)
        {
            return SuccessCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.AdwsPortConnectivity,
                "AD Web Services (9389)",
                $"{sanitizedHost}:{AdwsPort} bağlantısı başarılı.");
        }

        return FailedCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.AdwsPortConnectivity,
            "AD Web Services (9389)",
            $"{sanitizedHost}:{AdwsPort} bağlantısı başarısız.",
            "Uygulama sunucusundan domain controller üzerindeki AD Web Services TCP 9389 portuna erişim verin.",
            AdDeletedObjectRestoreReadinessCommandBuilder.BuildTestNetConnectionCommand(sanitizedHost),
            null);
    }

    private async Task<AdDeletedObjectRestoreReadinessCheck> CheckRecycleBinFeatureAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        string domainController,
        CancellationToken cancellationToken)
    {
        var result = await powerShellProbe.CheckRecycleBinFeatureAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            return SuccessCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.RecycleBinFeature,
                "AD Recycle Bin Feature",
                "AD Recycle Bin Feature etkin.");
        }

        var message = ContainsToken(result.ErrorSummary, AdDeletedObjectRestoreReadinessPowerShellProbe.RecycleBinDisabledErrorToken)
            ? "AD Recycle Bin Feature etkin değil."
            : "AD Recycle Bin Feature durumu doğrulanamadı.";

        return FailedCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.RecycleBinFeature,
            "AD Recycle Bin Feature",
            message,
            "AD Recycle Bin Feature forest seviyesinde etkinleştirilmelidir. Bu işlem AD yöneticisi tarafından bilinçli olarak yapılmalıdır.",
            AdDeletedObjectRestoreReadinessCommandBuilder.BuildEnableRecycleBinCommand(
                request.DomainFqdn,
                domainController),
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
            var credentialMode = !string.IsNullOrWhiteSpace(request.ServiceAccountUserName)
                && !string.IsNullOrWhiteSpace(request.ServiceAccountPassword)
                    ? "Service account"
                    : "Process identity";

            return SuccessCheck(
                AdDeletedObjectRestoreReadinessCheckKeys.ServiceAccountAdwsRead,
                "ADWS temel okuma",
                $"{credentialMode} ile ADWS üzerinden temel AD okuma doğrulandı.");
        }

        var remediation = "Service account kimlik bilgilerini, ADWS erişimini ve yetkilerini kontrol edin.";
        var message = ResolveAdwsReadFailureMessage(result.ErrorSummary);

        return FailedCheck(
            AdDeletedObjectRestoreReadinessCheckKeys.ServiceAccountAdwsRead,
            "ADWS temel okuma",
            message,
            remediation,
            $"Get-ADRootDSE -Server {AdDeletedObjectRestoreReadinessCommandBuilder.SanitizeHost(domainController)}",
            result.ErrorSummary);
    }

    private static AdDeletedObjectRestoreReadinessCheck CreateRestorePermissionWarningCheck() =>
        new(
            AdDeletedObjectRestoreReadinessCheckKeys.RestorePermissionNotVerified,
            AdDeletedObjectRestoreReadinessCheckStatuses.Warning,
            "Geri yükleme yetkisi",
            "Restore yetkisi destructive olmayan bir testle tam doğrulanamadı.",
            "Service account'a silinen nesneleri geri yükleme yetkisi verildiğinden emin olun.",
            null,
            false,
            null);

    private static AdDeletedObjectRestoreReadinessCheck CreateSettingsCheck(
        string message,
        string remediation) =>
        new(
            AdDeletedObjectRestoreReadinessCheckKeys.AdManagementSettings,
            AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
            "AD yönetim ayarları",
            message,
            remediation,
            null,
            true,
            null);

    private static AdDeletedObjectRestoreReadinessCheck CreateNotCheckedCheck(
        string key,
        string title,
        string message) =>
        new(
            key,
            AdDeletedObjectRestoreReadinessCheckStatuses.NotChecked,
            title,
            message,
            null,
            null,
            false,
            null);

    private static AdDeletedObjectRestoreReadinessCheck SuccessCheck(
        string key,
        string title,
        string message) =>
        new(
            key,
            AdDeletedObjectRestoreReadinessCheckStatuses.Success,
            title,
            message,
            null,
            null,
            false,
            null);

    private static AdDeletedObjectRestoreReadinessCheck FailedCheck(
        string key,
        string title,
        string message,
        string remediation,
        string? command,
        string? details) =>
        new(
            key,
            AdDeletedObjectRestoreReadinessCheckStatuses.Failed,
            title,
            message,
            remediation,
            command,
            true,
            details);

    private static AdDeletedObjectRestoreReadinessResult BuildResult(
        IReadOnlyList<AdDeletedObjectRestoreReadinessCheck> checks,
        DateTimeOffset checkedAt,
        string? domainController)
    {
        var blockingReasons = checks
            .Where(check => check.IsBlocking && check.Status == AdDeletedObjectRestoreReadinessCheckStatuses.Failed)
            .ToList();
        var warnings = checks
            .Where(check =>
                check.Status == AdDeletedObjectRestoreReadinessCheckStatuses.Warning
                || (check.Key == AdDeletedObjectRestoreReadinessCheckKeys.RestorePermissionNotVerified
                    && check.Status == AdDeletedObjectRestoreReadinessCheckStatuses.Warning))
            .ToList();

        var isReady = blockingReasons.Count == 0;
        var status = !isReady
            ? AdDeletedObjectRestoreReadinessStatuses.NotReady
            : warnings.Count > 0
                ? AdDeletedObjectRestoreReadinessStatuses.Warning
                : AdDeletedObjectRestoreReadinessStatuses.Ready;

        var summaryMessage = status switch
        {
            AdDeletedObjectRestoreReadinessStatuses.Ready => ReadySummaryMessage,
            AdDeletedObjectRestoreReadinessStatuses.Warning => WarningSummaryMessage,
            _ => blockingReasons.FirstOrDefault()?.Message ?? NotReadySummaryMessage,
        };

        return new AdDeletedObjectRestoreReadinessResult(
            isReady,
            status,
            summaryMessage,
            blockingReasons,
            warnings,
            checks,
            checkedAt,
            domainController);
    }

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

    private static string ResolveAdwsReadFailureMessage(string? errorSummary)
    {
        if (string.IsNullOrWhiteSpace(errorSummary))
        {
            return "Service account ADWS üzerinden temel AD okuma doğrulamasını tamamlayamadı.";
        }

        var lower = errorSummary.ToLowerInvariant();
        if (lower.Contains("access is denied", StringComparison.Ordinal)
            || lower.Contains("access denied", StringComparison.Ordinal)
            || lower.Contains("unauthorized", StringComparison.Ordinal)
            || lower.Contains("insufficient", StringComparison.Ordinal))
        {
            return "Service account ADWS üzerinden temel AD okuma için yetkilendirme hatası aldı.";
        }

        if (lower.Contains("timed out", StringComparison.Ordinal)
            || lower.Contains("timeout", StringComparison.Ordinal))
        {
            return "Service account ADWS üzerinden temel AD okuma zaman aşımına uğradı.";
        }

        if (lower.Contains("unavailable", StringComparison.Ordinal)
            || lower.Contains("cannot connect", StringComparison.Ordinal)
            || lower.Contains("could not connect", StringComparison.Ordinal)
            || lower.Contains("server not operational", StringComparison.Ordinal))
        {
            return "Service account ADWS sunucusuna bağlanamadı.";
        }

        return "Service account ADWS üzerinden temel AD okuma doğrulamasını tamamlayamadı.";
    }

    private static bool ContainsToken(string? value, string token) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(token, StringComparison.Ordinal);
}
