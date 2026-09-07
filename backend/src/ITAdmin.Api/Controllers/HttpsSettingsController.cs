using System.Security.Claims;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.SystemHttps;
using ITAdmin.Api.HostAgent;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Security;
using ITAdmin.HostAgent.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITAdmin.Api.Controllers;

/// <summary>
/// Manages the site's HTTPS binding from the application. An administrator uploads a PFX and its
/// password; the privileged ITAdmin Host Agent imports the certificate into the machine store and
/// binds it - the web application never gains certificate-store or IIS write access.
/// </summary>
[ApiController]
[Route("api/system/https")]
[Authorize]
public sealed class HttpsSettingsController(
    IHostAgentClient hostAgentClient,
    IAuditLogWriter auditLogWriter,
    ILogger<HttpsSettingsController> logger) : ControllerBase
{
    // A PFX with a full chain is a few tens of KB; well under this and under the pipe frame cap.
    private const long MaxPfxBytes = 512 * 1024;

    [HttpGet("status")]
    [RequirePermission(PermissionCodes.SystemHttps.View)]
    public async Task<ActionResult<SystemHttpsStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.GetHttpsStatus,
                CorrelationId = HttpContext.TraceIdentifier,
            }, cancellationToken);

            return Ok(MapStatus(response, agentAvailable: true));
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "ITAdmin Host Agent was unavailable while reading HTTPS status.");
            return Ok(new SystemHttpsStatusResponse(
                AgentAvailable: false, Enabled: false, Port: 443, RedirectHttpToHttps: false,
                CertificateThumbprint: null, CertificateSubject: null, CertificateNotAfterUtc: null,
                Message: "The ITAdmin Host Agent could not be reached on this server."));
        }
    }

    [HttpPost("configure")]
    [RequirePermission(PermissionCodes.SystemHttps.Manage)]
    [RequestSizeLimit(MaxPfxBytes + (64 * 1024))]
    public async Task<ActionResult<ConfigureSystemHttpsResponse>> Configure(
        [FromForm] IFormFile? pfx,
        [FromForm] string? password,
        [FromForm] int httpsPort,
        [FromForm] bool redirectHttpToHttps,
        CancellationToken cancellationToken)
    {
        if (pfx is null || pfx.Length == 0)
        {
            return BadRequest(new { message = "A PFX file is required." });
        }

        if (pfx.Length > MaxPfxBytes)
        {
            return BadRequest(new { message = "The PFX file is larger than 512 KB." });
        }

        if (httpsPort is < 1 or > 65535)
        {
            return BadRequest(new { message = "httpsPort must be between 1 and 65535." });
        }

        byte[] pfxBytes;
        await using (var stream = new MemoryStream())
        {
            await pfx.CopyToAsync(stream, cancellationToken);
            pfxBytes = stream.ToArray();
        }

        try
        {
            var request = new HostAgentRequest
            {
                Operation = HostAgentOperation.ConfigureHttps,
                CorrelationId = HttpContext.TraceIdentifier,
                PfxBase64 = Convert.ToBase64String(pfxBytes),
                PfxPassword = password ?? string.Empty,
                HttpsPort = httpsPort,
                RedirectHttpToHttps = redirectHttpToHttps,
            };

            var response = await hostAgentClient.SendAsync(request, cancellationToken);

            if (response.Status is HostAgentResponseStatus.Rejected)
            {
                return BadRequest(new { message = response.Message });
            }

            if (response.Status is not HostAgentResponseStatus.Ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = response.Message });
            }

            await auditLogWriter.WriteAsync(new AuditLogWriteRequest
            {
                Action = "SystemHttpsConfigured",
                EntityName = "SystemHttps",
                EntityId = response.Https?.CertificateThumbprint,
                Description = $"HTTPS bound on port {response.Https?.Port ?? httpsPort}"
                              + (redirectHttpToHttps ? " with HTTP-to-HTTPS redirect." : "."),
                ActorUserId = ResolveActorUserId(),
                ActorUserName = User.Identity?.Name,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
            }, cancellationToken);

            var https = response.Https;
            return Ok(new ConfigureSystemHttpsResponse(
                Enabled: https?.Enabled ?? true,
                Port: https?.Port ?? httpsPort,
                RedirectHttpToHttps: https?.RedirectHttpToHttps ?? redirectHttpToHttps,
                CertificateThumbprint: https?.CertificateThumbprint,
                CertificateSubject: https?.CertificateSubject,
                CertificateNotAfterUtc: https?.CertificateNotAfterUtc,
                Message: response.Message));
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "ITAdmin Host Agent was unavailable while configuring HTTPS.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "The ITAdmin Host Agent could not be reached on this server.",
            });
        }
        finally
        {
            Array.Clear(pfxBytes);
        }
    }

    [HttpPost("disable")]
    [RequirePermission(PermissionCodes.SystemHttps.Manage)]
    public async Task<ActionResult<ConfigureSystemHttpsResponse>> Disable(CancellationToken cancellationToken)
    {
        try
        {
            var response = await hostAgentClient.SendAsync(new HostAgentRequest
            {
                Operation = HostAgentOperation.DisableHttps,
                CorrelationId = HttpContext.TraceIdentifier,
            }, cancellationToken);

            if (response.Status is not HostAgentResponseStatus.Ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = response.Message });
            }

            await auditLogWriter.WriteAsync(new AuditLogWriteRequest
            {
                Action = "SystemHttpsDisabled",
                EntityName = "SystemHttps",
                Description = "HTTPS binding and redirect removed; the site is HTTP-only.",
                ActorUserId = ResolveActorUserId(),
                ActorUserName = User.Identity?.Name,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
            }, cancellationToken);

            var https = response.Https;
            return Ok(new ConfigureSystemHttpsResponse(
                Enabled: https?.Enabled ?? false,
                Port: https?.Port ?? 443,
                RedirectHttpToHttps: https?.RedirectHttpToHttps ?? false,
                CertificateThumbprint: https?.CertificateThumbprint,
                CertificateSubject: https?.CertificateSubject,
                CertificateNotAfterUtc: https?.CertificateNotAfterUtc,
                Message: response.Message));
        }
        catch (HostAgentUnavailableException exception)
        {
            logger.LogWarning(exception, "ITAdmin Host Agent was unavailable while disabling HTTPS.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "The ITAdmin Host Agent could not be reached on this server.",
            });
        }
    }

    private static SystemHttpsStatusResponse MapStatus(HostAgentResponse response, bool agentAvailable)
    {
        var https = response.Https;
        return new SystemHttpsStatusResponse(
            AgentAvailable: agentAvailable,
            Enabled: https?.Enabled ?? false,
            Port: https?.Port ?? 443,
            RedirectHttpToHttps: https?.RedirectHttpToHttps ?? false,
            CertificateThumbprint: https?.CertificateThumbprint,
            CertificateSubject: https?.CertificateSubject,
            CertificateNotAfterUtc: https?.CertificateNotAfterUtc,
            Message: response.Message);
    }

    private Guid? ResolveActorUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
}
