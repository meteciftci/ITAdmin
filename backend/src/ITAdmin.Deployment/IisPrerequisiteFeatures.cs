namespace ITAdmin.Deployment;

/// <summary>
/// The authoritative set of Windows Server IIS role services ITAdmin needs.
///
/// <para>
/// Detection and installation both consume this list. Expanding IIS with
/// <c>IncludeAllSubFeature</c> is deliberately avoided: only the features required to host
/// the published ASP.NET Core app behind ANCM, serve static content, authenticate via the
/// application's own LDAP path, and manage the site with the WebAdministration module are
/// listed here.
/// </para>
/// </summary>
public static class IisPrerequisiteFeatures
{
    /// <summary>
    /// Ordered install list. Windows <c>Install-WindowsFeature</c> resolves dependencies; we still
    /// list each required leaf so detection cannot drift from installation.
    /// </summary>
    public static IReadOnlyList<IisFeatureRequirement> Required { get; } =
    [
        new("Web-Server",
            "IIS Web Server role — ASP.NET Core is hosted out-of-process behind ANCM."),
        new("Web-Default-Doc",
            "Default document support for the SPA entry point under wwwroot."),
        new("Web-Http-Errors",
            "HTTP error responses for requests that never reach the ASP.NET Core process."),
        new("Web-Static-Content",
            "Static file serving for the published frontend assets under wwwroot."),
        new("Web-Http-Logging",
            "IIS request logging for operational diagnosis alongside application Serilog logs."),
        new("Web-Stat-Compression",
            "Static compression for frontend assets."),
        new("Web-Filtering",
            "Request filtering — baseline IIS request hardening."),
        new("Web-Mgmt-Console",
            "IIS Manager console for operator visibility of sites, bindings, and app pools."),
        new("Web-Scripting-Tools",
            "IIS management scripts — provides the WebAdministration PowerShell module the installer uses."),
    ];

    public static IReadOnlyList<string> RequiredNames { get; } =
        Required.Select(feature => feature.Name).ToArray();
}

/// <summary>One Windows feature the installer must detect and, when asked, install.</summary>
public sealed record IisFeatureRequirement(string Name, string Reason);
