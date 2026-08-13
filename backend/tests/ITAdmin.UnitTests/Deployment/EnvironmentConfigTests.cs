using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

public sealed class EnvironmentConfigTests
{
    /// <summary>
    /// What a first install actually produces: a database target, an IIS site, and HTTP. No FQDN,
    /// no certificate, no TLS.
    /// </summary>
    private static EnvironmentConfig FreshInstallConfig() => new()
    {
        Database = new DatabaseConfig
        {
            Host = "db.example.com",
            Port = 5432,
            Name = "itadmin",
            Username = "itadmin_app",
        },
        Iis = new IisConfig { SiteName = "ITAdmin", AppPoolName = "ITAdmin" },
    };

    /// <summary>The later state, after an administrator configures HTTPS from Settings.</summary>
    private static EnvironmentConfig HttpsConfiguredConfig() => FreshInstallConfig() with
    {
        ApplicationFqdn = "itadmin.example.com",
        Web = new WebHostingConfig
        {
            HttpHostHeader = "itadmin.example.com",
            Https = new HttpsConfig
            {
                Enabled = true,
                Port = 443,
                CertificateThumbprint = new string('A', 40),
                RedirectHttpToHttps = true,
            },
        },
    };

    [Fact]
    public void Validate_FreshHttpOnlyInstall_Passes()
    {
        // The point of the HTTP-only contract: an installation is complete and valid with no
        // certificate, no public host name, and no DNS.
        var result = FreshInstallConfig().Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_HttpsConfiguredLater_Passes() =>
        Assert.True(HttpsConfiguredConfig().Validate().IsValid);

    [Fact]
    public void Validate_MissingApplicationFqdn_IsNotAnInstallBlocker()
    {
        var result = (FreshInstallConfig() with { ApplicationFqdn = null }).Validate();

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, error => error.Contains("applicationFqdn", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("not a hostname")]
    [InlineData("http://itadmin.example.com")]
    [InlineData("itadmin.example.com/path")]
    public void Validate_MalformedApplicationFqdn_IsRejectedWhenSupplied(string fqdn)
    {
        var result = (FreshInstallConfig() with { ApplicationFqdn = fqdn }).Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("applicationFqdn", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_IpAddressAsDatabaseHost_IsAccepted() =>
        // Databases are commonly reached by address rather than name.
        Assert.True((FreshInstallConfig() with
        {
            Database = FreshInstallConfig().Database with { Host = "192.0.2.10" },
        }).Validate().IsValid);

    [Fact]
    public void Validate_HttpsEnabledWithoutCertificate_IsRejected()
    {
        var result = (HttpsConfiguredConfig() with
        {
            Web = new WebHostingConfig { Https = new HttpsConfig { Enabled = true } },
        }).Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("certificateThumbprint", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_HttpsEnabledWithoutApplicationFqdn_IsRejected()
    {
        var result = (FreshInstallConfig() with
        {
            Web = new WebHostingConfig
            {
                Https = new HttpsConfig { Enabled = true, CertificateThumbprint = new string('B', 40) },
            },
        }).Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("applicationFqdn", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("tooshort")]
    [InlineData("NOTHEXADECIMALZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void Validate_MalformedCertificateThumbprint_IsRejected(string thumbprint)
    {
        var result = (HttpsConfiguredConfig() with
        {
            Web = HttpsConfiguredConfig().Web with
            {
                Https = new HttpsConfig { Enabled = true, CertificateThumbprint = thumbprint },
            },
        }).Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("certificateThumbprint", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void Validate_DatabasePortOutOfRange_IsRejected(int port)
    {
        var result = (FreshInstallConfig() with
        {
            Database = FreshInstallConfig().Database with { Port = port },
        }).Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("database.port", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MissingDatabaseIdentity_IsRejected()
    {
        var result = (FreshInstallConfig() with
        {
            Database = new DatabaseConfig { Host = "db.example.com", Port = 5432 },
        }).Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("database.name", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("database.username", StringComparison.Ordinal));
    }

    [Fact]
    public void Config_HoldsNoSecretMaterial()
    {
        // The database password, JWT key, setup key, and directory bind password live in the ACL'd
        // secret store; this file is only non-sensitive coordinates.
        Assert.Empty(FreshInstallConfig().FindDisallowedSecretFields());
        Assert.Empty(HttpsConfiguredConfig().FindDisallowedSecretFields());
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var config = HttpsConfiguredConfig();

        var restored = EnvironmentConfig.FromJson(config.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(config.ApplicationFqdn, restored!.ApplicationFqdn);
        Assert.Equal(config.Database.Host, restored.Database.Host);
        Assert.Equal(config.Web.Https.CertificateThumbprint, restored.Web.Https.CertificateThumbprint);
        Assert.True(restored.Validate().IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ corrupt")]
    public void FromJson_MalformedInput_ReturnsNull(string? json) =>
        Assert.Null(EnvironmentConfig.FromJson(json));

    [Fact]
    public void Defaults_ContainNoOrganizationSpecificValues()
    {
        // A default-constructed config must be empty of environment identity. Only technology
        // standards (PostgreSQL 5432, HTTP 80, HTTPS 443) and the product's own name may be defaulted.
        var config = new EnvironmentConfig();

        Assert.Null(config.ApplicationFqdn);
        Assert.Null(config.Web.HttpHostHeader);
        Assert.Equal(string.Empty, config.Database.Host);
        Assert.Equal(string.Empty, config.Database.Name);
        Assert.Equal(string.Empty, config.Database.Username);
        Assert.Null(config.Web.Https.CertificateThumbprint);
        Assert.Equal(5432, config.Database.Port);
        Assert.Equal(80, config.Web.HttpPort);
        Assert.Equal(443, config.Web.Https.Port);
        Assert.False(config.Web.Https.Enabled);
        Assert.False(config.Validate().IsValid);
    }

    [Fact]
    public void Defaults_ContainNoAcceptanceEnvironmentValues()
    {
        var json = new EnvironmentConfig().ToJson();

        foreach (var term in new[]
                 {
                     "muglabb", "mugla.bel.tr", "SRV-ITADMIN", "10.5.1.", "10.30.40.",
                     "dc1.", "dc2.", "DC=muglabb",
                 })
        {
            Assert.DoesNotContain(term, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AccessUrls_PreferTheMachinesOwnNames_WithNoProductDefault()
    {
        var urls = WebAccessUrls.Build(
            new WebHostingConfig(),
            machineName: "example-host",
            machineFqdn: "example-host.corp.example.com",
            ipAddresses: ["192.0.2.25"]);

        Assert.Equal(
        [
            "http://example-host.corp.example.com/",
            "http://example-host/",
            "http://192.0.2.25/",
        ], urls);
    }

    [Fact]
    public void AccessUrls_NonDefaultPort_IsIncluded()
    {
        var urls = WebAccessUrls.Build(
            new WebHostingConfig { HttpPort = 8080 },
            machineName: "example-host",
            machineFqdn: null);

        Assert.Equal(["http://example-host:8080/"], urls);
    }

    [Fact]
    public void AccessUrls_HostHeaderBinding_OnlyAdvertisesThatName()
    {
        // A host header binding answers on exactly one name; printing the machine name too would
        // send the operator to a URL that returns 404.
        var urls = WebAccessUrls.Build(
            new WebHostingConfig { HttpHostHeader = "itadmin.example.com" },
            machineName: "example-host",
            machineFqdn: "example-host.corp.example.com");

        Assert.Equal(["http://itadmin.example.com/"], urls);
    }

    [Fact]
    public void AccessUrls_NothingDiscoverable_FallsBackToLocalhost() =>
        Assert.Equal(
            ["http://localhost/"],
            WebAccessUrls.Build(new WebHostingConfig(), machineName: null, machineFqdn: null));
}
