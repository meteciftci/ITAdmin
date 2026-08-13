using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITAdmin.Deployment;

/// <summary>
/// The machine-specific configuration an installer needs in order to host ITAdmin. This is the
/// mirror image of <see cref="ReleaseManifest"/>: everything here varies per customer, and none
/// of it may ever appear in a release artifact.
///
/// <para>
/// Scope boundary - this covers only what the <em>installer</em> must know to host the app:
/// how to reach the database, and how to publish the site over HTTP. The application's operational
/// settings (Primary Authentication Directory, AD Management search bases, preferred controllers,
/// creation defaults) have their own settings contract inside the product. The one exception is the
/// directory <em>coordinates</em> the installer validates during first install, which are handed to
/// the application's own setup service and persisted by it - not duplicated here.
/// </para>
///
/// <para>
/// No secrets live in this file. The database password, JWT signing key, setup key, and directory
/// bind password are held in the separately-ACL'd secret store; this file only records
/// non-sensitive coordinates.
/// </para>
/// </summary>
public sealed record EnvironmentConfig
{
    public const int CurrentSchemaVersion = 2;
    public const string FileName = "environment.json";

    /// <summary>Technology-standard default. Not an organization value.</summary>
    public const int DefaultPostgreSqlPort = 5432;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Public host name, once an administrator has configured one in ITAdmin Settings. Empty after
    /// a first install: initial hosting is HTTP on the machine's own name, and choosing a public
    /// FQDN is a later, deliberate act that comes with DNS and a certificate.
    /// </summary>
    [JsonPropertyName("applicationFqdn")]
    public string? ApplicationFqdn { get; init; }

    [JsonPropertyName("web")]
    public WebHostingConfig Web { get; init; } = new();

    [JsonPropertyName("database")]
    public DatabaseConfig Database { get; init; } = new();

    [JsonPropertyName("iis")]
    public IisConfig Iis { get; init; } = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static EnvironmentConfig? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EnvironmentConfig>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Validates the operator-supplied environment before any machine change is made, so a bad
    /// input fails during preflight rather than half-way through configuring IIS.
    /// </summary>
    public ConfigValidationResult Validate()
    {
        var errors = new List<string>();

        if (SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Unsupported environment config schemaVersion {SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        // Deliberately optional. Requiring an FQDN here is what previously forced a certificate,
        // DNS, and a host header to exist before anyone could log in for the first time.
        if (!string.IsNullOrWhiteSpace(ApplicationFqdn) && !IsPlausibleHostName(ApplicationFqdn))
        {
            errors.Add("applicationFqdn, when set, must be a valid host name.");
        }

        errors.AddRange(Web.Validate(ApplicationFqdn));

        if (!IsPlausibleHostName(Database.Host))
        {
            errors.Add("database.host must be a valid host name or IP address.");
        }

        if (Database.Port is < 1 or > 65535)
        {
            errors.Add("database.port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(Database.Name))
        {
            errors.Add("database.name is required.");
        }

        if (string.IsNullOrWhiteSpace(Database.Username))
        {
            errors.Add("database.username is required.");
        }

        if (string.IsNullOrWhiteSpace(Iis.SiteName))
        {
            errors.Add("iis.siteName is required.");
        }

        if (string.IsNullOrWhiteSpace(Iis.AppPoolName))
        {
            errors.Add("iis.appPoolName is required.");
        }

        return new ConfigValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Detects secret-looking values that must never be written to this file, so a future change
    /// cannot quietly start persisting a password next to the non-sensitive coordinates.
    /// </summary>
    public IReadOnlyList<string> FindDisallowedSecretFields()
    {
        var offenders = new List<string>();
        var json = ToJson();

        foreach (var forbidden in new[]
                 {
                     "password", "secret", "connectionstring", "jwtkey", "setupkey", "bindpassword",
                 })
        {
            if (json.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(forbidden);
            }
        }

        return offenders;
    }

    internal static bool IsPlausibleHostName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 253)
        {
            return false;
        }

        return Uri.CheckHostName(value) is UriHostNameType.Dns
            or UriHostNameType.IPv4
            or UriHostNameType.IPv6;
    }

    internal static bool IsPlausibleThumbprint(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length == 40
        && value.All(Uri.IsHexDigit);
}

/// <summary>
/// How the site is published. Initial installation is HTTP-only by design.
///
/// <para>
/// The previous model made HTTPS an install-time gate: without a certificate already imported and a
/// resolvable FQDN, installation could not complete, so a machine that was otherwise perfectly
/// installable failed at the last step over a task that belongs to a different team on a different
/// day. Splitting them means an administrator can reach a working ITAdmin over HTTP on the server's
/// own name within minutes, and then configure hostname, certificate, and redirect from Settings -
/// applied by the privileged host agent, which is the only component allowed to touch IIS.
/// </para>
/// </summary>
public sealed record WebHostingConfig
{
    /// <summary>Technology-standard default, not an organization value.</summary>
    public const int DefaultHttpPort = 80;

    [JsonPropertyName("httpPort")]
    public int HttpPort { get; init; } = DefaultHttpPort;

    /// <summary>
    /// Host header on the HTTP binding. Null/empty means "all unassigned host names", which is what
    /// a first install wants: the site answers on the machine name, its FQDN, and its addresses
    /// without anybody having to decide which one is canonical yet.
    /// </summary>
    [JsonPropertyName("httpHostHeader")]
    public string? HttpHostHeader { get; init; }

    [JsonPropertyName("https")]
    public HttpsConfig Https { get; init; } = new();

    internal IReadOnlyList<string> Validate(string? applicationFqdn)
    {
        var errors = new List<string>();

        if (HttpPort is < 1 or > 65535)
        {
            errors.Add("web.httpPort must be between 1 and 65535.");
        }

        if (!string.IsNullOrWhiteSpace(HttpHostHeader)
            && !EnvironmentConfig.IsPlausibleHostName(HttpHostHeader))
        {
            errors.Add("web.httpHostHeader, when set, must be a valid host name.");
        }

        if (!Https.Enabled)
        {
            return errors;
        }

        // HTTPS is never reached by an initial install, but the shape is validated here so the
        // host agent's later reconciliation has one definition of a valid configuration.
        if (string.IsNullOrWhiteSpace(Https.CertificateThumbprint))
        {
            errors.Add("web.https.certificateThumbprint is required when HTTPS is enabled.");
        }
        else if (!EnvironmentConfig.IsPlausibleThumbprint(Https.CertificateThumbprint))
        {
            errors.Add("web.https.certificateThumbprint must be a 40-character hexadecimal SHA-1 thumbprint.");
        }

        if (Https.Port is < 1 or > 65535)
        {
            errors.Add("web.https.port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(applicationFqdn))
        {
            errors.Add("applicationFqdn is required before HTTPS can be enabled.");
        }

        if (Https.RedirectHttpToHttps && !Https.Enabled)
        {
            errors.Add("web.https.redirectHttpToHttps requires HTTPS to be enabled.");
        }

        return errors;
    }
}

public sealed record HttpsConfig
{
    /// <summary>False on a fresh install. Enabled later from ITAdmin Settings.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; } = 443;

    /// <summary>
    /// Thumbprint of a certificate already present in the machine's LocalMachine\My store. The
    /// certificate and its private key are never carried in the repository or the release artifact.
    /// </summary>
    [JsonPropertyName("certificateThumbprint")]
    public string? CertificateThumbprint { get; init; }

    [JsonPropertyName("redirectHttpToHttps")]
    public bool RedirectHttpToHttps { get; init; }
}

public sealed record DatabaseConfig
{
    [JsonPropertyName("host")]
    public string Host { get; init; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; init; } = EnvironmentConfig.DefaultPostgreSqlPort;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    /// <summary>Npgsql SSL mode, e.g. Prefer/Require. No credential material.</summary>
    [JsonPropertyName("sslMode")]
    public string SslMode { get; init; } = "Prefer";
}

public sealed record IisConfig
{
    [JsonPropertyName("siteName")]
    public string SiteName { get; init; } = "ITAdmin";

    [JsonPropertyName("appPoolName")]
    public string AppPoolName { get; init; } = "ITAdmin";
}

public sealed record ConfigValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>
/// Turns a hosting configuration plus what was discovered about the machine into the URLs an
/// operator should actually try. Pure and environment-neutral: every name comes from the host the
/// installer is running on, never from a value baked into the product.
/// </summary>
public static class WebAccessUrls
{
    /// <summary>
    /// Builds the list of URLs to print at the end of an install, most specific first, de-duplicated.
    /// </summary>
    public static IReadOnlyList<string> Build(
        WebHostingConfig web,
        string? machineName,
        string? machineFqdn,
        IEnumerable<string>? ipAddresses = null)
    {
        ArgumentNullException.ThrowIfNull(web);

        var scheme = web.Https.Enabled ? "https" : "http";
        var port = web.Https.Enabled ? web.Https.Port : web.HttpPort;
        var defaultPort = web.Https.Enabled ? 443 : 80;

        var hosts = new List<string>();

        // A host header, once set, is the only name the binding answers on - so it is the only
        // name worth printing.
        if (!string.IsNullOrWhiteSpace(web.HttpHostHeader) && !web.Https.Enabled)
        {
            hosts.Add(web.HttpHostHeader.Trim());
        }
        else
        {
            AddIfUsable(hosts, machineFqdn);
            AddIfUsable(hosts, machineName);
            foreach (var address in ipAddresses ?? [])
            {
                AddIfUsable(hosts, address);
            }
        }

        if (hosts.Count == 0)
        {
            hosts.Add("localhost");
        }

        var urls = new List<string>();
        foreach (var host in hosts)
        {
            var authority = port == defaultPort ? host : $"{host}:{port}";
            var url = $"{scheme}://{authority}/";
            if (!urls.Contains(url, StringComparer.OrdinalIgnoreCase))
            {
                urls.Add(url);
            }
        }

        return urls;

        static void AddIfUsable(List<string> hosts, string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var trimmed = candidate.Trim();
            if (EnvironmentConfig.IsPlausibleHostName(trimmed)
                && !hosts.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                hosts.Add(trimmed);
            }
        }
    }
}
