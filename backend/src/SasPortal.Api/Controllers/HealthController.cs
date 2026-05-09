using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SasPortal.Api.Contracts.Health;
using SasPortal.Application.Abstractions.Services;

namespace SasPortal.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IReadinessService readinessService, ILogger<HealthController> logger)
    : ControllerBase
{
    private const string HealthyStatus = "Healthy";
    private const string UnhealthyStatus = "Unhealthy";

    [HttpGet("readiness")]
    [AllowAnonymous]
    public async Task<ActionResult<ReadinessResponse>> Readiness(CancellationToken cancellationToken)
    {
        var traceId = HttpContext.TraceIdentifier;
        var checkedAt = DateTime.UtcNow;
        const bool apiAvailable = true;

        var result = await readinessService.CheckAsync(cancellationToken);

        if (!result.IsHealthy)
        {
            if (result.ExceptionForLog is not null)
            {
                if (result.LogExceptionAsError)
                {
                    logger.LogError(
                        result.ExceptionForLog,
                        "Readiness check failed. TraceId: {TraceId}",
                        traceId);
                }
                else
                {
                    logger.LogWarning(
                        result.ExceptionForLog,
                        "Readiness check failed. TraceId: {TraceId}",
                        traceId);
                }
            }
            else if (!result.DatabaseAvailable)
            {
                logger.LogWarning(
                    "Readiness check failed (database not reachable). TraceId: {TraceId}",
                    traceId);
            }
            else if (!result.LdapAvailable)
            {
                logger.LogWarning(
                    "Readiness check failed (LDAP not available). TraceId: {TraceId}",
                    traceId);
            }
            else
            {
                logger.LogWarning(
                    "Readiness check failed. TraceId: {TraceId}",
                    traceId);
            }

            var unhealthy = new ReadinessResponse(
                Status: UnhealthyStatus,
                ApiAvailable: apiAvailable,
                DatabaseAvailable: result.DatabaseAvailable,
                LdapAvailable: result.LdapAvailable,
                Message: result.Message,
                TraceId: traceId,
                CheckedAt: checkedAt);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, unhealthy);
        }

        var healthy = new ReadinessResponse(
            Status: HealthyStatus,
            ApiAvailable: apiAvailable,
            DatabaseAvailable: true,
            LdapAvailable: true,
            Message: "Service is ready.",
            TraceId: traceId,
            CheckedAt: checkedAt);

        return Ok(healthy);
    }
}
