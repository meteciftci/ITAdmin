using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ITAdmin.Api.Security;

/// <summary>
/// Resolves the <c>Secure</c> flag for auth cookies.
///
/// <para>
/// In production the flag is normally <c>true</c> so cookies stay HTTPS-only even when the app
/// sits behind a reverse proxy that terminates TLS (IIS/ARR) and forwarded headers are
/// misconfigured. The one exception is the HTTP-only commissioning window: a fresh install runs
/// as production but has no HTTPS listener yet (HTTPS is configured afterwards from
/// Settings -> HTTPS), and forcing <c>Secure</c> there means the browser silently drops the
/// session cookie and nobody can sign in to finish setup. That window is detected by the absence
/// of the <c>Https:Enabled</c> / <c>Https:RedirectEnabled</c> configuration the deploy script
/// sets once an HTTPS binding exists.
/// </para>
///
/// <para>
/// Outside production the flag simply follows <see cref="HttpRequest.IsHttps"/> so
/// <c>http://localhost</c> development keeps working without extra configuration.
/// </para>
/// </summary>
public static class AuthCookieSecurityResolver
{
    public static bool ResolveSecure(HttpRequest request)
    {
        var services = request.HttpContext?.RequestServices;
        var environmentName = services?.GetService<IHostEnvironment>()?.EnvironmentName;

        var configuration = services?.GetService<IConfiguration>();
        var httpsConfigured =
            (configuration?.GetValue("Https:Enabled", false) ?? false)
            || (configuration?.GetValue("Https:RedirectEnabled", false) ?? false);

        return ResolveSecure(request.IsHttps, environmentName, httpsConfigured);
    }

    public static bool ResolveSecure(bool isHttpsRequest, string? environmentName, bool httpsConfigured = true)
    {
        if (isHttpsRequest)
        {
            return true;
        }

        var isProduction = string.Equals(
            environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase);

        // Production keeps auth cookies Secure-only so a TLS-terminating proxy with broken
        // forwarded headers cannot downgrade the session - but only once HTTPS actually exists.
        // During the HTTP-only commissioning window the flag has to follow the request or the
        // operator cannot sign in to configure HTTPS in the first place.
        return isProduction && httpsConfigured;
    }
}
