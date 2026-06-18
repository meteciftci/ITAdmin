using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using ITAdmin.Api.Security;

namespace ITAdmin.UnitTests.Security;

public sealed class ForwardedHeadersSetupTests
{
    [Fact]
    public void Apply_with_empty_configuration_keeps_safe_loopback_defaults()
    {
        var options = new ForwardedHeadersOptions();
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        ForwardedHeadersSetup.Apply(options, configuration);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Single(options.KnownProxies);
        Assert.Equal(IPAddress.IPv6Loopback, options.KnownProxies[0]);
    }

    [Fact]
    public void Apply_adds_configured_known_proxies_and_networks()
    {
        var options = new ForwardedHeadersOptions();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = "203.0.113.10",
            ["ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0/8",
        });

        ForwardedHeadersSetup.Apply(options, configuration);

        Assert.Contains(IPAddress.Parse("203.0.113.10"), options.KnownProxies);
        Assert.Contains(
            options.KnownIPNetworks,
            network => network.BaseAddress.Equals(IPAddress.Parse("10.0.0.0")) && network.PrefixLength == 8);
    }

    [Fact]
    public void Apply_ignores_invalid_proxy_and_network_values()
    {
        var options = new ForwardedHeadersOptions();
        var knownProxyCountBefore = options.KnownProxies.Count;
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = "not-an-ip",
            ["ForwardedHeaders:KnownNetworks:0"] = "not-a-cidr",
            ["ForwardedHeaders:KnownNetworks:1"] = "10.0.0.0",
        });

        ForwardedHeadersSetup.Apply(options, configuration);

        Assert.Equal(knownProxyCountBefore, options.KnownProxies.Count);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("10.0.0.0", false)]
    [InlineData("10.0.0.0/8", true)]
    [InlineData(" 192.168.1.0/24 ", true)]
    [InlineData("10.0.0.0/64", false)]
    public void TryParseNetwork_validates_cidr_values(string? value, bool expected)
    {
        Assert.Equal(expected, ForwardedHeadersSetup.TryParseNetwork(value, out _));
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
