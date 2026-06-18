namespace ITAdmin.Application.Common.AdManagement;

public static class AdComputerOperatingSystemOptionsNormalizer
{
    public static IReadOnlyList<string> NormalizeDistinctSorted(
        IEnumerable<string?> values,
        int maxCount = AdComputerDirectoryLimits.OperatingSystemOptionsMaxCount)
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var trimmed = value.Trim();
            seen.TryAdd(trimmed, trimmed);

            if (seen.Count >= maxCount)
            {
                break;
            }
        }

        var result = seen.Values.ToList();
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }
}
