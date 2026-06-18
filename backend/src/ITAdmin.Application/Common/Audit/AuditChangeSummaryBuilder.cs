using System.Text;

namespace ITAdmin.Application.Common.Audit;

public static class AuditChangeSummaryBuilder
{
    public const int DefaultMaxLength = 2000;
    private const int LongValueThreshold = 120;

    public static string BuildChangesSegment(IReadOnlyList<AuditFieldChange> changes)
    {
        if (changes.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(changes.Count);
        foreach (var change in changes)
        {
            parts.Add(FormatChange(change));
        }

        return string.Join(", ", parts);
    }

    public static string BuildUpdateDescription(
        string prefix,
        IReadOnlyList<AuditFieldChange> changes,
        int maxLength = DefaultMaxLength)
    {
        var segment = BuildChangesSegment(changes);
        var description = string.IsNullOrWhiteSpace(segment)
            ? prefix.TrimEnd('.', ' ')
            : $"{prefix.TrimEnd('.', ' ')}. Changes: {segment}.";

        return Truncate(description, maxLength);
    }

    public static AuditFieldChange SensitiveChanged(string fieldName, bool hadValue, bool hasValue) =>
        hadValue switch
        {
            true when !hasValue => new AuditFieldChange
            {
                FieldName = fieldName,
                IsSensitive = true,
                DisplayMode = AuditChangeDisplayMode.Cleared,
            },
            false when hasValue => new AuditFieldChange
            {
                FieldName = fieldName,
                IsSensitive = true,
                DisplayMode = AuditChangeDisplayMode.ChangedOnly,
            },
            true when hasValue => new AuditFieldChange
            {
                FieldName = fieldName,
                IsSensitive = true,
                DisplayMode = AuditChangeDisplayMode.ChangedOnly,
            },
            _ => new AuditFieldChange
            {
                FieldName = fieldName,
                IsSensitive = true,
                DisplayMode = AuditChangeDisplayMode.ChangedOnly,
            },
        };

    public static AuditFieldChange PublicField(
        string fieldName,
        string? oldValue,
        string? newValue,
        bool treatAsLongText = false)
    {
        var oldNormalized = NormalizeValue(oldValue);
        var newNormalized = NormalizeValue(newValue);

        if (string.Equals(oldNormalized, newNormalized, StringComparison.Ordinal))
        {
            return new AuditFieldChange
            {
                FieldName = fieldName,
                OldValue = oldNormalized,
                NewValue = newNormalized,
            };
        }

        if (treatAsLongText
            || (oldNormalized?.Length ?? 0) > LongValueThreshold
            || (newNormalized?.Length ?? 0) > LongValueThreshold)
        {
            return new AuditFieldChange
            {
                FieldName = fieldName,
                DisplayMode = AuditChangeDisplayMode.ChangedOnly,
            };
        }

        return new AuditFieldChange
        {
            FieldName = fieldName,
            OldValue = oldNormalized,
            NewValue = newNormalized,
            DisplayMode = AuditChangeDisplayMode.OldNew,
        };
    }

    private static string FormatChange(AuditFieldChange change)
    {
        if (change.IsSensitive)
        {
            return change.DisplayMode switch
            {
                AuditChangeDisplayMode.Cleared => $"{change.FieldName} cleared",
                _ => $"{change.FieldName} changed",
            };
        }

        return change.DisplayMode switch
        {
            AuditChangeDisplayMode.ChangedOnly => $"{change.FieldName} changed",
            AuditChangeDisplayMode.Cleared => $"{change.FieldName} cleared",
            AuditChangeDisplayMode.OldNew => $"{change.FieldName} {FormatValue(change.OldValue)} -> {FormatValue(change.NewValue)}",
            _ => $"{change.FieldName} changed",
        };
    }

    private static string FormatValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();

    private static string? NormalizeValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..(maxLength - 3)]}...";
}
