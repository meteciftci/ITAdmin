using Microsoft.Net.Http.Headers;

namespace SasPortal.Api.Middlewares;

/// <summary>
/// Adds baseline security headers to every response (API and static SPA assets).
/// Headers are written in an <see cref="HttpResponse.OnStarting"/> callback so they apply
/// consistently regardless of which downstream component produced the response.
/// <c>Cache-Control: no-store</c> is only forced for API error and auth responses so the
/// static frontend asset caching behavior is left intact.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// SPA/Vite-friendly baseline: build output uses external script/css bundles, so scripts
    /// stay locked to 'self'. 'unsafe-inline' is limited to styles because React components and
    /// UI primitives rely on inline style attributes. frame-ancestors 'none' blocks framing.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; " +
        "connect-src 'self'; " +
        "form-action 'self'";

    public const string PermissionsPolicy = "camera=(), microphone=(), geolocation=()";

    private const string ApiPathPrefix = "/api";
    private const string AuthApiPathPrefix = "/api/auth";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            ApplyHeaders(httpContext);
            return Task.CompletedTask;
        }, context);

        await next(context);
    }

    public static void ApplyHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers[HeaderNames.XFrameOptions] = "DENY";
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;

        if (ShouldForceNoStore(context.Request.Path, context.Response.StatusCode))
        {
            headers[HeaderNames.CacheControl] = "no-store";
        }
    }

    /// <summary>
    /// API error responses and auth lifecycle responses (login/refresh/logout/me) must never
    /// be cached by browsers or intermediaries; everything else keeps its own cache behavior.
    /// </summary>
    public static bool ShouldForceNoStore(PathString path, int statusCode)
    {
        if (!path.StartsWithSegments(ApiPathPrefix))
        {
            return false;
        }

        return statusCode >= StatusCodes.Status400BadRequest
            || path.StartsWithSegments(AuthApiPathPrefix);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder) =>
        builder.UseMiddleware<SecurityHeadersMiddleware>();
}
