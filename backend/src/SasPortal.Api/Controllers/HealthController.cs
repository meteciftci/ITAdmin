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

        var dbResult = await readinessService.CheckDatabaseAsync(cancellationToken);

        if (!dbResult.IsHealthy)
        {
            if (dbResult.ExceptionForLog is not null)
            {
                if (dbResult.LogExceptionAsError)
                {
                    logger.LogError(
                        dbResult.ExceptionForLog,
                        "Readiness check failed. TraceId: {TraceId}",
                        traceId);
                }
                else
                {
                    logger.LogWarning(
                        dbResult.ExceptionForLog,
                        "Readiness check failed. TraceId: {TraceId}",
                        traceId);
                }
            }
            else
            {
                logger.LogWarning(
                    "Readiness check failed (database not reachable). TraceId: {TraceId}",
                    traceId);
            }

            var unhealthy = new ReadinessResponse(
                Status: UnhealthyStatus,
                ApiAvailable: apiAvailable,
                DatabaseAvailable: dbResult.DatabaseAvailable,
                Message: dbResult.Message,
                TraceId: traceId,
                CheckedAt: checkedAt);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, unhealthy);
        }

        var healthy = new ReadinessResponse(
            Status: HealthyStatus,
            ApiAvailable: apiAvailable,
            DatabaseAvailable: true,
            Message: "Service is ready.",
            TraceId: traceId,
            CheckedAt: checkedAt);

        return Ok(healthy);
    }
}
