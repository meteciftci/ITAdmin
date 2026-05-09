using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;
using SasPortal.Persistence.Common;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class ReadinessService(AppDbContext context) : IReadinessService
{
    public async Task<ReadinessDatabaseResult> CheckDatabaseAsync(CancellationToken cancellationToken)
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
