using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace ITAdmin.Api.Security;

/// <summary>
/// Applies forwarded headers configuration for IIS / reverse proxy deployments.
/// Only <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> are honored, and only from
/// proxies listed in configuration (<c>ForwardedHeaders:KnownProxies</c> /
/// <c>ForwardedHeaders:KnownNetworks</c>). When configuration is empty the framework
/// defaults remain in place (loopback only), so arbitrary clients can never spoof
/// their IP or scheme by sending forwarded headers.
/// </summary>
public static class ForwardedHeadersSetup
{
    public const string KnownProxiesConfigurationKey = "ForwardedHeaders:KnownProxies";
    public const string KnownNetworksConfigurationKey = "ForwardedHeaders:KnownNetworks";

    public static void Apply(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        foreach (var rawProxy in ReadValues(configuration, KnownProxiesConfigurationKey))
        {
            if (IPAddress.TryParse(rawProxy, out var proxyAddress))
            {
                options.KnownProxies.Add(proxyAddress);
            }
        }

        foreach (var rawNetwork in ReadValues(configuration, KnownNetworksConfigurationKey))
        {
            if (TryParseNetwork(rawNetwork, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }
    }

    public static bool TryParseNetwork(string? value, out IPNetwork network)
    {
        network = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return IPNetwork.TryParse(value.Trim(), out network);
    }

    private static IEnumerable<string> ReadValues(IConfiguration configuration, string key)
    {
        return configuration.GetSection(key)
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim());
    }
}
