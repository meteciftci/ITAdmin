using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ITAdmin.Api.Controllers;

internal static class LicenseManagementActorResolver
{
    private const string JwtSubClaimType = "sub";

    internal static string? ResolveActorUserName(ClaimsPrincipal principal)
    {
        if (!string.IsNullOrWhiteSpace(principal.Identity?.Name))
        {
            return principal.Identity!.Name;
        }

        var nameClaim = principal.FindFirst(ClaimTypes.Name) ?? principal.FindFirst("name");
        return string.IsNullOrWhiteSpace(nameClaim?.Value) ? null : nameClaim.Value.Trim();
    }

    internal static Guid? ResolveActorUserId(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtSubClaimType)?.Value;

        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    internal static string? ResolveIpAddress(ControllerBase controller)
    {
        var ip = controller.HttpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? null : ip;
    }

    internal static string? ResolveUserAgent(ControllerBase controller)
    {
        var userAgent = controller.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }
}
