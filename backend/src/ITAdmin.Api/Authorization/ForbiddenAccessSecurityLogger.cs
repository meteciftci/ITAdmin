using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Security;

namespace ITAdmin.Api.Authorization;

public sealed class ForbiddenAccessSecurityLogger(
    ISecurityLogWriter securityLogWriter,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ForbiddenAccessSecurityLogger> logger)
{
    internal const string ForbiddenAccessLoggedItemKey = "ITAdmin.SecurityLog.ForbiddenAccessLogged";

    public async Task TryLogPermissionDeniedAsync(
        AuthorizationHandlerContext context,
        string requiredPermissionDescription,
        CancellationToken cancellationToken = default)
    {
        var httpContext = ResolveHttpContext(context);
        if (httpContext is null)
        {
            return;
        }

        if (httpContext.Items.ContainsKey(ForbiddenAccessLoggedItemKey))
        {
            return;
        }

        httpContext.Items[ForbiddenAccessLoggedItemKey] = true;

        var request = httpContext.Request;
        var description =
            $"Access denied. Required permission: {requiredPermissionDescription}. " +
            $"{request.Method} {request.Path.Value ?? "/"}.";

        var endpointDisplayName = httpContext.GetEndpoint()?.DisplayName;
        if (!string.IsNullOrWhiteSpace(endpointDisplayName))
        {
            description += $" Endpoint: {endpointDisplayName.Trim()}.";
        }

        await TryWriteSecurityLogAsync(
            new SecurityLogWriteRequest
            {
                EventType = SecurityLogEventTypes.ForbiddenAccess,
                Severity = "Warning",
                UserId = ResolveUserId(context.User),
                UserName = ResolveUserName(context.User),
                IpAddress = ResolveIpAddress(httpContext),
                UserAgent = ResolveUserAgent(httpContext),
                Description = description,
            },
            cancellationToken);
    }

    public async Task TryLogCsrfValidationFailedAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        if (httpContext.Items.ContainsKey(ForbiddenAccessLoggedItemKey))
        {
            return;
        }

        httpContext.Items[ForbiddenAccessLoggedItemKey] = true;

        var request = httpContext.Request;
        var description =
            $"CSRF token validation failed. {request.Method} {request.Path.Value ?? "/"}.";

        await TryWriteSecurityLogAsync(
            new SecurityLogWriteRequest
            {
                EventType = SecurityLogEventTypes.CsrfValidationFailed,
                Severity = "Warning",
                UserId = ResolveUserId(httpContext.User),
                UserName = ResolveUserName(httpContext.User),
                IpAddress = ResolveIpAddress(httpContext),
                UserAgent = ResolveUserAgent(httpContext),
                Description = description,
            },
            cancellationToken);
    }

    private async Task TryWriteSecurityLogAsync(
        SecurityLogWriteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await securityLogWriter.TryWriteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write security log for event {EventType}", request.EventType);
        }
    }

    private HttpContext? ResolveHttpContext(AuthorizationHandlerContext context)
    {
        if (context.Resource is HttpContext resourceHttpContext)
        {
            return resourceHttpContext;
        }

        if (context.Resource is AuthorizationFilterContext mvcContext)
        {
            return mvcContext.HttpContext;
        }

        return httpContextAccessor.HttpContext;
    }

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var rawUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private static string? ResolveUserName(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(user.Identity?.Name))
        {
            return user.Identity!.Name.Trim();
        }

        var nameClaim = user.FindFirst(ClaimTypes.Name) ?? user.FindFirst("name");
        return string.IsNullOrWhiteSpace(nameClaim?.Value) ? null : nameClaim.Value.Trim();
    }

    private static string? ResolveIpAddress(HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? null : ip;
    }

    private static string? ResolveUserAgent(HttpContext httpContext)
    {
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }
}
