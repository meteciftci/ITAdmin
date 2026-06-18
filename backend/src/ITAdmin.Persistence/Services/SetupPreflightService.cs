using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using ITAdmin.Persistence.Common;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed class SetupPreflightService(
    AppDbContext context,
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ISetupKeyValidator setupKeyValidator) : ISetupPreflightService
{
    public async Task<SetupPreflightResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<SetupPreflightCheck>();

        await AddDatabaseChecksAsync(checks, cancellationToken);
        AddJwtChecks(checks);
        AddSetupKeyHashChecks(checks);
        AddDataProtectionChecks(checks);
        AddRuntimeMetadataChecks(checks);

        return new SetupPreflightResult(
            checks,
            checks.All(check => check.Status == SetupPreflightCheckStatuses.Ok));
    }

    private async Task AddDatabaseChecksAsync(List<SetupPreflightCheck> checks, CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                checks.Add(new SetupPreflightCheck(
                    SetupPreflightCheckKeys.DatabaseReachable,
                    SetupPreflightCheckStatuses.Error,
                    SetupPreflightMessageKeys.DatabaseUnreachable,
                    null));
                return;
            }

            checks.Add(new SetupPreflightCheck(
                SetupPreflightCheckKeys.DatabaseReachable,
                SetupPreflightCheckStatuses.Ok,
                SetupPreflightMessageKeys.DatabaseReachable,
                null));

            try
            {
                await context.ApplicationSettings
                    .AsNoTracking()
                    .Take(1)
                    .CountAsync(cancellationToken);
                checks.Add(new SetupPreflightCheck(
                    SetupPreflightCheckKeys.DatabaseQuery,
                    SetupPreflightCheckStatuses.Ok,
                    SetupPreflightMessageKeys.DatabaseQuerySucceeded,
                    null));
            }
            catch (Exception)
            {
                checks.Add(new SetupPreflightCheck(
                    SetupPreflightCheckKeys.DatabaseQuery,
                    SetupPreflightCheckStatuses.Error,
                    SetupPreflightMessageKeys.DatabaseQueryFailed,
                    null));
            }
        }
        catch (Exception exception) when (DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception))
        {
            checks.Add(new SetupPreflightCheck(
                SetupPreflightCheckKeys.DatabaseReachable,
                SetupPreflightCheckStatuses.Error,
                SetupPreflightMessageKeys.DatabaseUnreachable,
                null));
        }
    }

    private void AddJwtChecks(List<SetupPreflightCheck> checks)
    {
        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.JwtKeyConfigured,
            configuration["Jwt:Key"],
            SetupPreflightMessageKeys.JwtKeyConfigured,
            SetupPreflightMessageKeys.JwtKeyMissing);

        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.JwtIssuerConfigured,
            configuration["Jwt:Issuer"],
            SetupPreflightMessageKeys.JwtIssuerConfigured,
            SetupPreflightMessageKeys.JwtIssuerMissing);

        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.JwtAudienceConfigured,
            configuration["Jwt:Audience"],
            SetupPreflightMessageKeys.JwtAudienceConfigured,
            SetupPreflightMessageKeys.JwtAudienceMissing);
    }

    private void AddSetupKeyHashChecks(List<SetupPreflightCheck> checks)
    {
        var configuredHash = configuration[SetupKeyHashValidator.ConfigurationKey];

        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.SetupKeyHashConfigured,
            configuredHash,
            SetupPreflightMessageKeys.SetupKeyHashConfigured,
            SetupPreflightMessageKeys.SetupKeyHashMissing);

        if (!setupKeyValidator.IsHashConfigured(configuredHash))
        {
            return;
        }

        checks.Add(new SetupPreflightCheck(
            SetupPreflightCheckKeys.SetupKeyHashValidFormat,
            setupKeyValidator.IsValidHashFormat(configuredHash)
                ? SetupPreflightCheckStatuses.Ok
                : SetupPreflightCheckStatuses.Error,
            setupKeyValidator.IsValidHashFormat(configuredHash)
                ? SetupPreflightMessageKeys.SetupKeyHashValidFormat
                : SetupPreflightMessageKeys.SetupKeyHashInvalidFormat,
            null));
    }

    private void AddDataProtectionChecks(List<SetupPreflightCheck> checks)
    {
        var applicationName = NormalizeConfigurationValue(configuration["DataProtection:ApplicationName"]);
        var keysPath = NormalizeConfigurationValue(configuration["DataProtection:KeysPath"]);
        var isProductionLike = hostEnvironment.IsProduction() || hostEnvironment.IsStaging();

        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.DataProtectionApplicationNameConfigured,
            applicationName,
            SetupPreflightMessageKeys.DataProtectionApplicationNameConfigured,
            SetupPreflightMessageKeys.DataProtectionApplicationNameMissing);

        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.DataProtectionKeysPathConfigured,
            keysPath,
            SetupPreflightMessageKeys.DataProtectionKeysPathConfigured,
            SetupPreflightMessageKeys.DataProtectionKeysPathMissing);

        if (string.IsNullOrWhiteSpace(keysPath))
        {
            return;
        }

        var pathExists = Directory.Exists(keysPath);
        checks.Add(new SetupPreflightCheck(
            SetupPreflightCheckKeys.DataProtectionKeysPathExists,
            pathExists
                ? SetupPreflightCheckStatuses.Ok
                : ResolveDataProtectionPathStatus(isProductionLike),
            pathExists
                ? SetupPreflightMessageKeys.DataProtectionKeysPathExists
                : SetupPreflightMessageKeys.DataProtectionKeysPathMissingOnDisk,
            null));

        if (!pathExists)
        {
            return;
        }

        var isWritable = IsDirectoryWritable(keysPath);
        checks.Add(new SetupPreflightCheck(
            SetupPreflightCheckKeys.DataProtectionKeysPathWritable,
            isWritable
                ? SetupPreflightCheckStatuses.Ok
                : ResolveDataProtectionPathStatus(isProductionLike),
            isWritable
                ? SetupPreflightMessageKeys.DataProtectionKeysPathWritable
                : SetupPreflightMessageKeys.DataProtectionKeysPathNotWritable,
            null));
    }

    private void AddRuntimeMetadataChecks(List<SetupPreflightCheck> checks)
    {
        var environmentName = hostEnvironment.EnvironmentName;
        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.EnvironmentName,
            environmentName,
            SetupPreflightMessageKeys.EnvironmentNameAvailable,
            SetupPreflightMessageKeys.EnvironmentNameMissing,
            detail: string.IsNullOrWhiteSpace(environmentName) ? null : environmentName);

        var applicationName = hostEnvironment.ApplicationName;
        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.ApplicationName,
            applicationName,
            SetupPreflightMessageKeys.ApplicationNameAvailable,
            SetupPreflightMessageKeys.ApplicationNameMissing,
            detail: string.IsNullOrWhiteSpace(applicationName) ? null : applicationName);

        var applicationVersion = typeof(SetupPreflightService).Assembly.GetName().Version?.ToString();
        AddConfiguredCheck(
            checks,
            SetupPreflightCheckKeys.ApplicationVersion,
            applicationVersion,
            SetupPreflightMessageKeys.ApplicationVersionAvailable,
            SetupPreflightMessageKeys.ApplicationVersionMissing,
            detail: string.IsNullOrWhiteSpace(applicationVersion) ? null : applicationVersion);
    }

    private static string ResolveDataProtectionPathStatus(bool isProductionLike) =>
        isProductionLike ? SetupPreflightCheckStatuses.Error : SetupPreflightCheckStatuses.Warning;

    private static string? NormalizeConfigurationValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddConfiguredCheck(
        List<SetupPreflightCheck> checks,
        string key,
        string? value,
        string configuredMessageKey,
        string missingMessageKey,
        string? detail = null)
    {
        var isConfigured = !string.IsNullOrWhiteSpace(value);
        checks.Add(new SetupPreflightCheck(
            key,
            isConfigured ? SetupPreflightCheckStatuses.Ok : SetupPreflightCheckStatuses.Error,
            isConfigured ? configuredMessageKey : missingMessageKey,
            detail));
    }

    private static bool IsDirectoryWritable(string directoryPath)
    {
        var probeFilePath = Path.Combine(directoryPath, $".itadmin-preflight-{Guid.NewGuid():N}.tmp");

        try
        {
            using (File.Create(probeFilePath))
            {
            }

            File.Delete(probeFilePath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (File.Exists(probeFilePath))
            {
                try
                {
                    File.Delete(probeFilePath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
