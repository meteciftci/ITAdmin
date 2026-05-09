using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Common;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class ReadinessService(
    AppDbContext context,
    ILdapService ldapService,
    ISecretProtector secretProtector) : IReadinessService
{
    public Task<ReadinessDatabaseResult> CheckDatabaseAsync(CancellationToken cancellationToken) =>
        ProbeDatabaseAsync(cancellationToken);

    public async Task<ReadinessResult> CheckAsync(CancellationToken cancellationToken)
    {
        var databaseResult = await ProbeDatabaseAsync(cancellationToken);
        if (!databaseResult.IsHealthy)
        {
            return new ReadinessResult(
                DatabaseAvailable: databaseResult.DatabaseAvailable,
                LdapAvailable: false,
                IsHealthy: false,
                Message: databaseResult.Message,
                ExceptionForLog: databaseResult.ExceptionForLog,
                LogExceptionAsError: databaseResult.LogExceptionAsError);
        }

        var ldapSetting = await context.LdapSettings
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (ldapSetting is null)
        {
            return new ReadinessResult(
                DatabaseAvailable: true,
                LdapAvailable: false,
                IsHealthy: false,
                Message: "LDAP settings are not configured.",
                ExceptionForLog: null,
                LogExceptionAsError: false);
        }

        string bindPassword;
        try
        {
            bindPassword = secretProtector.Unprotect(ldapSetting.EncryptedBindPassword);
        }
        catch (Exception exception)
        {
            return new ReadinessResult(
                DatabaseAvailable: true,
                LdapAvailable: false,
                IsHealthy: false,
                Message: "LDAP service is temporarily unavailable.",
                ExceptionForLog: exception,
                LogExceptionAsError: false);
        }

        try
        {
            var bindResult = await ldapService.ValidateBindAsync(
                new LdapBindValidationRequest
                {
                    Host = ldapSetting.Host,
                    Port = ldapSetting.Port,
                    UseSsl = ldapSetting.UseSsl,
                    BindUserName = ldapSetting.BindUserName,
                    BindUserDomain = ldapSetting.BindUserDomain,
                    BindPassword = bindPassword
                },
                cancellationToken);

            if (!bindResult.IsValid)
            {
                return new ReadinessResult(
                    DatabaseAvailable: true,
                    LdapAvailable: false,
                    IsHealthy: false,
                    Message: "LDAP service is temporarily unavailable.",
                    ExceptionForLog: null,
                    LogExceptionAsError: false);
            }

            return new ReadinessResult(
                DatabaseAvailable: true,
                LdapAvailable: true,
                IsHealthy: true,
                Message: "Service is ready.",
                ExceptionForLog: null,
                LogExceptionAsError: false);
        }
        catch (Exception exception)
        {
            return new ReadinessResult(
                DatabaseAvailable: true,
                LdapAvailable: false,
                IsHealthy: false,
                Message: "LDAP service is temporarily unavailable.",
                ExceptionForLog: exception,
                LogExceptionAsError: true);
        }
    }

    private async Task<ReadinessDatabaseResult> ProbeDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return new ReadinessDatabaseResult(
                    DatabaseAvailable: true,
                    Message: "Service is ready.",
                    IsHealthy: true,
                    ExceptionForLog: null,
                    LogExceptionAsError: false);
            }

            return new ReadinessDatabaseResult(
                DatabaseAvailable: false,
                Message: "Database service is temporarily unavailable.",
                IsHealthy: false,
                ExceptionForLog: null,
                LogExceptionAsError: false);
        }
        catch (Exception exception)
        {
            if (DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception))
            {
                return new ReadinessDatabaseResult(
                    DatabaseAvailable: false,
                    Message: "Database service is temporarily unavailable.",
                    IsHealthy: false,
                    ExceptionForLog: exception,
                    LogExceptionAsError: false);
            }

            return new ReadinessDatabaseResult(
                DatabaseAvailable: false,
                Message: "Service readiness check failed.",
                IsHealthy: false,
                ExceptionForLog: exception,
                LogExceptionAsError: true);
        }
    }
}
