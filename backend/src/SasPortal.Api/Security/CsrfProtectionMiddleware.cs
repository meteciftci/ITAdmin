using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace SasPortal.Api.Security;

/// <summary>
/// Enforces double-submit CSRF validation for cookie-authenticated unsafe requests under <c>/api</c>.
/// Safe HTTP methods, auth-lifecycle endpoints (login, refresh, logout) and requests that do not
/// carry the access-token cookie are bypassed so 401/403 decisions remain with the auth pipeline.
/// The <c>Authorization</c> header has no effect on CSRF enforcement: SAS Portal is cookie-only.
/// </summary>
public sealed class CsrfProtectionMiddleware
{
    private const string FailureTitle = "CSRF token validation failed.";

    // Cached payload: middleware always writes the same problem body, no per-request data.
    // Plain JSON is used (rather than ProblemDetails via Results.Problem) so the middleware
    // does not depend on IProblemDetailsService / RequestServices for its 403 response.
    private static readonly byte[] FailureBody = JsonSerializer.SerializeToUtf8Bytes(new
    {
        type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
        title = FailureTitle,
        status = StatusCodes.Status403Forbidden,
    });

    private readonly RequestDelegate _next;

    public CsrfProtectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!CsrfProtection.ShouldValidateRequest(request))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (CsrfProtection.TryValidateRequest(request))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        await WriteForbiddenAsync(context).ConfigureAwait(false);
    }

    private static async Task WriteForbiddenAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        context.Response.ContentLength = FailureBody.Length;
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";

        await context.Response.Body.WriteAsync(FailureBody).ConfigureAwait(false);
    }
}

/// <summary>
/// Pipeline extension that mirrors the framework convention for opt-in middleware.
/// </summary>
public static class CsrfProtectionMiddlewareExtensions
{
    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder builder) =>
        builder.UseMiddleware<CsrfProtectionMiddleware>();
}
