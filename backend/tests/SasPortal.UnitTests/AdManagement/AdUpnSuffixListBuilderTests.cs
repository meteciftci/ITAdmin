using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdUpnSuffixListBuilderTests
{
    [Fact]
    public void Build_DeduplicatesCaseInsensitiveAndPreservesSource()
    {
        var result = AdUpnSuffixListBuilder.Build(
            [
                new DiscoveredUpnSuffix("Mugla.Bel.TR", AdUpnSuffixSources.Forest),
                new DiscoveredUpnSuffix("mugla.bel.tr", AdUpnSuffixSources.Forest),
                new DiscoveredUpnSuffix("corp.local", AdUpnSuffixSources.Domain),
            ],
            domainFqdn: null,
            defaultNamingContextDnsSuffix: null,
            baseDnDnsSuffix: null,
            settingsOnlyFallback: false);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item is { Value: "mugla.bel.tr", Source: AdUpnSuffixSources.Forest });
        Assert.Contains(result.Items, item => item is { Value: "corp.local", Source: AdUpnSuffixSources.Domain });
        Assert.Null(result.Warning);
    }

    [Fact]
    public void Build_AdReadSuccessWithEmptyForestAndDomainFqdn_HasNoWarning()
    {
        var result = AdUpnSuffixListBuilder.Build(
            [],
            domainFqdn: "muglabb.lcl",
            defaultNamingContextDnsSuffix: null,
            baseDnDnsSuffix: null,
            settingsOnlyFallback: false);

        Assert.Single(result.Items);
        Assert.Equal("muglabb.lcl", result.Items[0].Value);
        Assert.Equal(AdUpnSuffixSources.Domain, result.Items[0].Source);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void Build_AddsDefaultNamingContextDerivedSuffix()
    {
        var result = AdUpnSuffixListBuilder.Build(
            [],
            domainFqdn: null,
            defaultNamingContextDnsSuffix: "mugla.bel.tr",
            baseDnDnsSuffix: null,
            settingsOnlyFallback: false);

        Assert.Single(result.Items);
        Assert.Equal("mugla.bel.tr", result.Items[0].Value);
        Assert.Equal(AdUpnSuffixSources.Domain, result.Items[0].Source);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void Build_SettingsOnlyFallback_ReturnsWarning()
    {
        var result = AdUpnSuffixListBuilder.Build(
            [],
            domainFqdn: "muglabb.lcl",
            defaultNamingContextDnsSuffix: null,
            baseDnDnsSuffix: null,
            settingsOnlyFallback: true);

        Assert.Single(result.Items);
        Assert.Equal("muglabb.lcl", result.Items[0].Value);
        Assert.Equal(AdUpnSuffixSources.Fallback, result.Items[0].Source);
        Assert.Equal(AdUpnSuffixListBuilder.ForestReadFallbackWarning, result.Warning);
    }

    [Fact]
    public void Build_ReturnsEmptyWhenNoSources()
    {
        var result = AdUpnSuffixListBuilder.Build(
            [],
            null,
            null,
            null,
            settingsOnlyFallback: false);

        Assert.Empty(result.Items);
        Assert.Null(result.Warning);
    }
}
