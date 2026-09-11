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
        return new(hasUserId ? userId : null, Limit(userName, 100),
            Limit(controller.HttpContext.Connection.RemoteIpAddress?.ToString(), 64),
            Limit(controller.Request.Headers.UserAgent.ToString(), 1024));
    }

    private static string? Limit(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ? null : normalized[..Math.Min(normalized.Length, maxLength)];
    }
}
