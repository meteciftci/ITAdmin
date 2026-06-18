namespace ITAdmin.Application.Common.AdManagement;

public static class AdScalarAttributeComparer
{
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool EqualsOrdinalIgnoreCase(string? left, string? right) =>
        string.Equals(NormalizeOptional(left), NormalizeOptional(right), StringComparison.OrdinalIgnoreCase);

    public static bool HasChanged(string? existingValue, string requestedValue) =>
        !EqualsOrdinalIgnoreCase(existingValue, requestedValue);
}
