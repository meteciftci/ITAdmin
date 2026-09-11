using System.Net.Mail;

namespace ITAdmin.Application.Common.LicenseManagement;

public static class LicenseRenewalRecipientParser
{
    private static readonly char[] Separators = [',', ';', '\r', '\n'];

    public static IReadOnlyList<string> Parse(params string?[] values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(static value => value!.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static bool IsValidEmail(string value) =>
        MailAddress.TryCreate(value, out var address)
        && string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> InvalidEmails(params string?[] values) =>
        Parse(values).Where(static value => !IsValidEmail(value)).ToArray();
}
