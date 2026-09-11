using System.Security.Claims;
using ITAdmin.Application.Common.Models.DnsManagement;
using Microsoft.AspNetCore.Mvc;

namespace ITAdmin.Api.Controllers;

internal static class DnsManagementActorResolver
{
    internal static DnsActorContext Resolve(ControllerBase controller)
    {
        var principal = controller.User;
        var rawUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        var hasUserId = Guid.TryParse(rawUserId, out var userId);
        var userName = principal.Identity?.Name ?? principal.FindFirst(ClaimTypes.Name)?.Value ?? principal.FindFirst("name")?.Value;
        return new(hasUserId ? userId : null, userName,
            controller.HttpContext.Connection.RemoteIpAddress?.ToString(),
            controller.Request.Headers.UserAgent.ToString());
    }
}
