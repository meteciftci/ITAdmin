using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.Infrastructure.Services;

public sealed class AdDeletedObjectRestoreReadinessPowerShellProbe : IAdDeletedObjectRestoreReadinessPowerShellProbe
{
    private const string ActiveDirectoryModuleName = "ActiveDirectory";
    internal const string ModuleMissingErrorToken = "ActiveDirectoryModuleNotFound";
    internal const string RestoreCommandMissingErrorToken = "RestoreAdObjectCommandNotFound";
    internal const string RecycleBinDisabledErrorToken = "RecycleBinFeatureDisabled";

    public Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckActiveDirectoryModuleAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => ExecuteScript(
                request,
                $@"
if (-not (Get-Module -ListAvailable -Name '{ActiveDirectoryModuleName}')) {{
    throw '{ModuleMissingErrorToken}'
}}
",
                cancellationToken),
            cancellationToken);

    public Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckRestoreAdObjectCommandAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => ExecuteScript(
                request,
                $@"
if (-not (Get-Module -ListAvailable -Name '{ActiveDirectoryModuleName}')) {{
    throw '{ModuleMissingErrorToken}'
}}
Import-Module {ActiveDirectoryModuleName} -ErrorAction Stop
if (-not (Get-Command Restore-ADObject -ErrorAction SilentlyContinue)) {{
    throw '{RestoreCommandMissingErrorToken}'
}}
",
                cancellationToken),
            cancellationToken);

    public Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckRecycleBinFeatureAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var server = AdDeletedObjectRestoreReadinessCommandBuilder.SanitizeHost(request.Server);
        var usesCredential = !string.IsNullOrWhiteSpace(request.ServiceAccountUserName)
            && !string.IsNullOrWhiteSpace(request.ServiceAccountPassword);

        var credentialParam = usesCredential ? "-Credential $credential" : string.Empty;
        var credentialSetupScript = usesCredential
            ? @"
$credential = New-Object System.Management.Automation.PSCredential($bindIdentity, $securePassword)
"
            : string.Empty;

        return Task.Run(
            () => ExecuteScript(
                request,
                $@"
if (-not (Get-Module -ListAvailable -Name '{ActiveDirectoryModuleName}')) {{
    throw '{ModuleMissingErrorToken}'
}}
Import-Module {ActiveDirectoryModuleName} -ErrorAction Stop
$server = '{server}'
{credentialSetupScript}
$feature = Get-ADOptionalFeature -Identity 'Recycle Bin Feature' -Server $server {credentialParam} -Properties EnabledScopes -ErrorAction Stop
if ($null -eq $feature.EnabledScopes -or $feature.EnabledScopes.Count -le 0) {{
    throw '{RecycleBinDisabledErrorToken}'
}}
",
                cancellationToken,
                usesCredential
                    ? CreateCredentialVariables(request)
                    : null),
            cancellationToken);
    }

    public Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckAdwsReadAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var server = AdDeletedObjectRestoreReadinessCommandBuilder.SanitizeHost(request.Server);
        var usesCredential = !string.IsNullOrWhiteSpace(request.ServiceAccountUserName)
            && !string.IsNullOrWhiteSpace(request.ServiceAccountPassword);

        var credentialScript = usesCredential
            ? @"
$credential = New-Object System.Management.Automation.PSCredential($bindIdentity, $securePassword)
Get-ADRootDSE -Server $server -Credential $credential -ErrorAction Stop | Out-Null
"
            : @"
Get-ADRootDSE -Server $server -ErrorAction Stop | Out-Null
";

        return Task.Run(
            () => ExecuteScript(
                request,
                $@"
if (-not (Get-Module -ListAvailable -Name '{ActiveDirectoryModuleName}')) {{
    throw '{ModuleMissingErrorToken}'
}}
Import-Module {ActiveDirectoryModuleName} -ErrorAction Stop
$server = '{server}'
{credentialScript}
",
                cancellationToken,
                usesCredential
                    ? CreateCredentialVariables(request)
                    : null),
            cancellationToken);
    }

    private static IReadOnlyDictionary<string, object>? CreateCredentialVariables(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request)
    {
        var bindIdentity = AdServiceAccountBindIdentity.Build(
            request.ServiceAccountUserName!,
            request.NetbiosDomainName);

        var securePassword = new SecureString();
        foreach (var character in request.ServiceAccountPassword!)
        {
            securePassword.AppendChar(character);
        }

        securePassword.MakeReadOnly();

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["bindIdentity"] = bindIdentity,
            ["securePassword"] = securePassword,
        };
    }

    private static AdDeletedObjectRestoreReadinessPowerShellProbeResult ExecuteScript(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        string script,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object>? variables = null)
    {
        try
        {
            var initialSessionState = InitialSessionState.CreateDefault();
            initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

            using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
            runspace.Open();

            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;

            if (variables is not null)
            {
                foreach (var (name, value) in variables)
                {
                    powerShell.AddCommand("Set-Variable")
                        .AddParameter("Name", name)
                        .AddParameter("Value", value)
                        .Invoke();
                    powerShell.Commands.Clear();
                }
            }

            powerShell.AddScript(script);
            InvokePowerShell(powerShell, request.Timeout, cancellationToken);

            if (powerShell.HadErrors)
            {
                return new AdDeletedObjectRestoreReadinessPowerShellProbeResult(
                    false,
                    AdLdapDiagnosticSanitizer.SanitizePowerShellErrorSummary(BuildErrorSummary(powerShell)),
                    null);
            }

            return new AdDeletedObjectRestoreReadinessPowerShellProbeResult(true, null, null);
        }
        catch (OperationCanceledException)
        {
            return new AdDeletedObjectRestoreReadinessPowerShellProbeResult(
                false,
                "PowerShell readiness check timed out.",
                null);
        }
        catch (Exception ex)
        {
            return new AdDeletedObjectRestoreReadinessPowerShellProbeResult(
                false,
                AdLdapDiagnosticSanitizer.SanitizePowerShellErrorSummary(ex.Message),
                null);
        }
    }

    private static void InvokePowerShell(
        PowerShell powerShell,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var invokeTask = Task.Run(() => powerShell.Invoke(), timeoutSource.Token);
            invokeTask.Wait(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                powerShell.Stop();
            }
            catch
            {
                // Best effort stop for timed out command.
            }

            throw;
        }
    }

    private static string BuildErrorSummary(PowerShell powerShell)
    {
        var messages = powerShell.Streams.Error
            .Select(error => error.ToString())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();

        if (messages.Length == 0)
        {
            return "PowerShell readiness check failed.";
        }

        return string.Join(" | ", messages);
    }
}
