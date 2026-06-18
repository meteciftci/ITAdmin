namespace ITAdmin.Application.Common.AdManagement;

public static class AdDeletedObjectRestoreReadinessCommandBuilder
{
    private const int AdwsPort = 9389;

    public static string BuildTestNetConnectionCommand(string? domainController) =>
        $"Test-NetConnection {SanitizeHost(domainController)} -Port {AdwsPort}";

    public static string BuildInstallRsatCommand() =>
        "Install-WindowsFeature RSAT-AD-PowerShell";

    public static string BuildEnableRecycleBinCommand(string? domainFqdn, string? domainController)
    {
        var target = EscapePowerShellString(SanitizeDomainFqdn(domainFqdn));
        var server = EscapePowerShellString(SanitizeHost(domainController));
        return
            $"Enable-ADOptionalFeature -Identity \"Recycle Bin Feature\" -Scope ForestOrConfigurationSet -Target \"{target}\" -Server \"{server}\" -Confirm:$false";
    }

    public static string SanitizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "dc.example.com";
        }

        var trimmed = host.Trim();
        var sanitized = new string(
            trimmed
                .Where(character => char.IsLetterOrDigit(character) || character is '.' or '-')
                .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "dc.example.com" : sanitized;
    }

    public static string SanitizeDomainFqdn(string? domainFqdn)
    {
        if (string.IsNullOrWhiteSpace(domainFqdn))
        {
            return "example.com";
        }

        var trimmed = domainFqdn.Trim();
        var sanitized = new string(
            trimmed
                .Where(character => char.IsLetterOrDigit(character) || character is '.' or '-')
                .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "example.com" : sanitized;
    }

    private static string EscapePowerShellString(string value) =>
        value.Replace("\"", "`\"", StringComparison.Ordinal);
}
