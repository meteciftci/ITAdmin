using Microsoft.AspNetCore.Mvc;
using AppModels = ITAdmin.Application.Common.Models;

namespace ITAdmin.Api.Controllers;

public abstract class AdManagementControllerBase : ControllerBase
{
    protected ActionResult MapDirectoryFailure(
        string messageKey,
        AppModels.AdDirectoryFailureKind? failureKind,
        IReadOnlyDictionary<string, object>? messageParams = null) =>
        failureKind switch
        {
            AppModels.AdDirectoryFailureKind.NotFound => NotFound(new { messageKey, messageParams }),
            AppModels.AdDirectoryFailureKind.InvalidRequest => BadRequest(new { messageKey, messageParams }),
            AppModels.AdDirectoryFailureKind.ConnectionFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { messageKey, messageParams }),
            _ => BadRequest(new { messageKey, messageParams }),
        };

    protected string? ResolveIpAddress()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? null : ip;
    }

    protected string? ResolveUserAgent()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }
}
