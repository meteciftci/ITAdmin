namespace ITAdmin.Application.Common.AdManagement;

public enum AdMappedAttributeLdapAction
{
    Skip,
    Replace,
    Delete,
}

public static class AdMappedAttributeLdapUpdatePlanner
{
    public static AdMappedAttributeLdapAction ResolveAction(
        string? requestedValue,
        IReadOnlyList<string> existingAdValues)
    {
        var normalizedRequested = NormalizeRequestedValue(requestedValue);
        var existing = GetPrimaryExistingValue(existingAdValues);

        if (string.IsNullOrWhiteSpace(normalizedRequested))
        {
            return string.IsNullOrWhiteSpace(existing)
                ? AdMappedAttributeLdapAction.Skip
                : AdMappedAttributeLdapAction.Delete;
        }

        if (string.Equals(existing, normalizedRequested, StringComparison.Ordinal))
        {
            return AdMappedAttributeLdapAction.Skip;
        }

        return AdMappedAttributeLdapAction.Replace;
    }

    public static string? NormalizeRequestedValue(string? requestedValue) =>
        string.IsNullOrWhiteSpace(requestedValue) ? null : requestedValue.Trim();

    public static string? GetPrimaryExistingValue(IReadOnlyList<string> existingAdValues) =>
        existingAdValues
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            ?.Trim();
}
