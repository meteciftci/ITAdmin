using Microsoft.Extensions.Hosting;

namespace ITAdmin.Api.Security;

/// <summary>
/// Resolves the <c>Secure</c> flag for auth cookies. In production the flag is always
/// <c>true</c> so cookies stay HTTPS-only even when the app sits behind a reverse proxy
/// that terminates TLS (IIS/ARR) and forwarded headers are misconfigured. Outside production
/// the flag follows <see cref="HttpRequest.IsHttps"/> so <c>http://localhost</c> development
/// keeps working without extra configuration.
/// </summary>
public static class AuthCookieSecurityResolver
{
    public static bool ResolveSecure(HttpRequest request)
    {
        var environmentName = request.HttpContext?.RequestServices?
            .GetService<IHostEnvironment>()?.EnvironmentName;

        return ResolveSecure(request.IsHttps, environmentName);
    }

    public static bool ResolveSecure(bool isHttpsRequest, string? environmentName)
    {
        if (isHttpsRequest)
        {
            return true;
        }

        return string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase);
    }
}
