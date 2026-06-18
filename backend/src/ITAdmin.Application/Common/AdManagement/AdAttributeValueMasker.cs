namespace ITAdmin.Application.Common.AdManagement;

public static class AdAttributeValueMasker
{
    private const string HiddenMask = "••••";

    public static IReadOnlyList<string> MaskValues(
        IReadOnlyList<string> values,
        bool isSensitive,
        string? maskingStrategy)
    {
        if (!isSensitive || values.Count == 0)
        {
            return values;
        }

        var strategy = string.IsNullOrWhiteSpace(maskingStrategy)
            ? "Hidden"
            : maskingStrategy.Trim();

        return strategy switch
        {
            "Hidden" or "None" => [HiddenMask],
            "Last4" => values.Select(MaskLast4).ToList(),
            "Phone" => values.Select(MaskPhone).ToList(),
            "Email" => values.Select(MaskEmail).ToList(),
            _ => [HiddenMask],
        };
    }

    private static string MaskLast4(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return HiddenMask;
        }

        return $"{HiddenMask}{trimmed[^4..]}";
    }

    private static string MaskPhone(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
        {
            return HiddenMask;
        }

        return $"{HiddenMask}{digits[^4..]}";
    }

    private static string MaskEmail(string value)
    {
        var atIndex = value.IndexOf('@');
        if (atIndex <= 1)
        {
            return HiddenMask;
        }

        return $"{value[0]}***{value[atIndex..]}";
    }
}
