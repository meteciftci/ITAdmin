namespace SasPortal.Application.Common.AdManagement;

public static class AdLdapUpdateDiagnosticLog
{
    public static string Format(
        string updateStep,
        string? attributeName,
        int? ldapResultCode,
        string? ldapErrorMessage,
        int? ldapExceptionErrorCode,
        Guid targetObjectGuid,
        string? targetDistinguishedName)
    {
        var parts = new List<string>
        {
            $"UpdateStep={updateStep}",
            $"TargetObjectGuid={targetObjectGuid:D}",
        };

        if (!string.IsNullOrWhiteSpace(targetDistinguishedName))
        {
            parts.Add($"TargetDistinguishedName={targetDistinguishedName}");
        }

        if (!string.IsNullOrWhiteSpace(attributeName))
        {
            parts.Add($"Attribute={attributeName}");
        }

        if (ldapResultCode is not null)
        {
            parts.Add($"LdapResultCode={ldapResultCode}");
        }

        if (ldapExceptionErrorCode is not null)
        {
            parts.Add($"LdapExceptionErrorCode={ldapExceptionErrorCode}");
        }

        if (!string.IsNullOrWhiteSpace(ldapErrorMessage))
        {
            parts.Add($"LdapErrorMessage={SanitizeDiagnosticMessage(ldapErrorMessage)}");
        }

        return string.Join("; ", parts);
    }

    private static string SanitizeDiagnosticMessage(string message)
    {
        var trimmed = message.Trim();
        if (trimmed.Length > 500)
        {
            trimmed = $"{trimmed[..500]}…";
        }

        return trimmed.Replace('\r', ' ').Replace('\n', ' ');
    }
}
