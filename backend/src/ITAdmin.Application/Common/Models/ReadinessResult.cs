namespace ITAdmin.Application.Common.Models;

/// <summary>
/// Aggregated readiness probe result for public health reporting (database + LDAP).
/// </summary>
public sealed record ReadinessResult(
    bool DatabaseAvailable,
    bool LdapAvailable,
    bool IsHealthy,
    string Message,
    Exception? ExceptionForLog,
    bool LogExceptionAsError);
