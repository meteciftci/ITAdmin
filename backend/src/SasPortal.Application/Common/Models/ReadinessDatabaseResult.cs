namespace SasPortal.Application.Common.Models;

/// <summary>
/// Result of a database connectivity probe for public readiness reporting.
/// </summary>
public sealed record ReadinessDatabaseResult(
    bool DatabaseAvailable,
    string Message,
    bool IsHealthy,
    Exception? ExceptionForLog,
    bool LogExceptionAsError);
