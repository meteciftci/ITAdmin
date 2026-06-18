using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Common.AdManagement;

public static class AdUpnSuffixListBuilder
{
    public const string ForestReadFallbackWarning =
        "UPN suffix listesi AD üzerinden okunamadı. Fallback değer gösteriliyor.";

    public static AdUpnSuffixesBuildResult Build(
        IEnumerable<DiscoveredUpnSuffix> discoveredSuffixes,
        string? domainFqdn,
        string? defaultNamingContextDnsSuffix,
        string? baseDnDnsSuffix,
        bool settingsOnlyFallback)
    {
        var items = new List<AdUpnSuffixItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? rawValue, string source)
        {
            var normalized = AdDefaultUpnSuffixNormalizer.Normalize(rawValue);
            if (string.IsNullOrWhiteSpace(normalized)
                || !AdDefaultUpnSuffixNormalizer.IsValidFormat(normalized)
                || !seen.Add(normalized))
            {
                return;
            }

            items.Add(new AdUpnSuffixItem(normalized, source));
        }

        foreach (var discovered in discoveredSuffixes)
        {
            Add(discovered.Value, discovered.Source);
        }

        var configuredSource = settingsOnlyFallback
            ? AdUpnSuffixSources.Fallback
            : AdUpnSuffixSources.Domain;
        Add(domainFqdn, configuredSource);
        Add(defaultNamingContextDnsSuffix, configuredSource);
        Add(baseDnDnsSuffix, configuredSource);

        if (items.Count > 0)
        {
            return new AdUpnSuffixesBuildResult(
                items,
                settingsOnlyFallback ? ForestReadFallbackWarning : null);
        }

        return new AdUpnSuffixesBuildResult(
            Array.Empty<AdUpnSuffixItem>(),
            settingsOnlyFallback ? ForestReadFallbackWarning : null);
    }
}

public sealed record DiscoveredUpnSuffix(string Value, string Source);

public sealed record AdUpnSuffixesBuildResult(
    IReadOnlyList<AdUpnSuffixItem> Items,
    string? Warning);
