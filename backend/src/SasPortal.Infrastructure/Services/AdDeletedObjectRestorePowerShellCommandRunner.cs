using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Models;

namespace SasPortal.Infrastructure.Services;

public sealed class AdDeletedObjectRestorePowerShellCommandRunner(
    ILogger<AdDeletedObjectRestorePowerShellCommandRunner> logger) : IAdDeletedObjectRestoreCommandRunner
{
    private const string RestoreAdObjectCommand = "Restore-ADObject";
    private const string ActiveDirectoryModuleName = "ActiveDirectory";
    private const string CredentialModeServiceAccount = "ServiceAccount";
    private const string CredentialModeProcessIdentity = "ProcessIdentity";
    internal const string ModuleMissingErrorToken = "ActiveDirectoryModuleNotFound";

    public Task<AdDeletedObjectRestoreCommandResult> ExecuteRestoreAsync(
        AdDeletedObjectRestoreCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        var credentialMode = ResolveCredentialMode(request);
        return Task.Run(
            () => ExecuteRestoreCore(request, credentialMode, cancellationToken),
            cancellationToken);
    }

    private AdDeletedObjectRestoreCommandResult ExecuteRestoreCore(
        AdDeletedObjectRestoreCommandRequest request,
        string credentialMode,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var initialSessionState = InitialSessionState.CreateDefault();
            initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

            using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
            runspace.Open();

            using var powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;

            powerShell.AddScript(
                $@"
if (-not (Get-Module -ListAvailable -Name '{ActiveDirectoryModuleName}')) {{
    throw '{ModuleMissingErrorToken}'
}}
Import-Module {ActiveDirectoryModuleName} -ErrorAction Stop
");
            var moduleResult = InvokePowerShell(powerShell, request.Timeout, cancellationToken);
            if (!moduleResult.IsSuccess)
            {
                stopwatch.Stop();
                return moduleResult with
                {
                    CredentialMode = credentialMode,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                };
            }

            powerShell.Commands.Clear();
            var command = powerShell
                .AddCommand(RestoreAdObjectCommand)
                .AddParameter("Identity", request.ObjectGuid.ToString("D"))
                .AddParameter("Server", request.Server)
                .AddParameter("Confirm", false);

            if (request.RestoreTargetMode == AdDeletedObjectRestoreTargetMode.TargetPath
                && !string.IsNullOrWhiteSpace(request.TargetPathDistinguishedName))
            {
                command.AddParameter("TargetPath", request.TargetPathDistinguishedName.Trim());
            }

            if (string.Equals(credentialMode, CredentialModeServiceAccount, StringComparison.Ordinal))
            {
                command.AddParameter("Credential", CreateCredential(request));
            }

            var restoreResult = InvokePowerShell(powerShell, request.Timeout, cancellationToken);
            stopwatch.Stop();

            if (!restoreResult.IsSuccess)
            {
                return restoreResult with
                {
                    CredentialMode = credentialMode,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                };
            }

            return new AdDeletedObjectRestoreCommandResult(
                true,
                credentialMode,
                stopwatch.ElapsedMilliseconds,
                0,
                null,
                null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "AD deleted object restore PowerShell invocation failed. ObjectGuid={ObjectGuid}",
                request.ObjectGuid);

            return new AdDeletedObjectRestoreCommandResult(
                false,
                credentialMode,
                stopwatch.ElapsedMilliseconds,
                null,
                AdLdapDiagnosticSanitizer.SanitizePowerShellErrorSummary(ex.Message),
                MapPowerShellFailureKind(ex.Message));
        }
    }

    private static AdDeletedObjectRestoreCommandResult InvokePowerShell(
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

            return new AdDeletedObjectRestoreCommandResult(
                false,
                string.Empty,
                0,
                null,
                "The AD restore command timed out.",
                AdDirectoryFailureKind.ConnectionFailed);
        }

        if (!powerShell.HadErrors)
        {
            return new AdDeletedObjectRestoreCommandResult(
                true,
                string.Empty,
                0,
                0,
                null,
                null);
        }

        var errorSummary = BuildErrorSummary(powerShell);
        return new AdDeletedObjectRestoreCommandResult(
            false,
            string.Empty,
            0,
            1,
            AdLdapDiagnosticSanitizer.SanitizePowerShellErrorSummary(errorSummary),
            MapPowerShellFailureKind(errorSummary));
    }

    private static string BuildErrorSummary(PowerShell powerShell)
    {
        var messages = powerShell.Streams.Error
            .Select(error => error.ToString())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();

        if (messages.Length == 0)
        {
            return "The AD restore command failed.";
        }

        return string.Join(" | ", messages);
    }

    private static PSCredential CreateCredential(AdDeletedObjectRestoreCommandRequest request)
    {
        var bindIdentity = AdServiceAccountBindIdentity.Build(
            request.ServiceAccountUserName,
            request.NetbiosDomainName);

        var securePassword = new SecureString();
        foreach (var character in request.ServiceAccountPassword!)
        {
            securePassword.AppendChar(character);
        }

        securePassword.MakeReadOnly();
        return new PSCredential(bindIdentity, securePassword);
    }

    private static string ResolveCredentialMode(AdDeletedObjectRestoreCommandRequest request) =>
        !string.IsNullOrWhiteSpace(request.ServiceAccountUserName)
        && !string.IsNullOrWhiteSpace(request.ServiceAccountPassword)
            ? CredentialModeServiceAccount
            : CredentialModeProcessIdentity;

    internal static AdDirectoryFailureKind MapPowerShellFailureKind(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return AdDirectoryFailureKind.InvalidRequest;
        }

        if (message.Contains(ModuleMissingErrorToken, StringComparison.Ordinal))
        {
            return AdDirectoryFailureKind.ConnectionFailed;
        }

        var lower = message.ToLowerInvariant();
        if (lower.Contains("timed out", StringComparison.Ordinal)
            || lower.Contains("timeout", StringComparison.Ordinal)
            || lower.Contains("unavailable", StringComparison.Ordinal)
            || lower.Contains("cannot connect", StringComparison.Ordinal)
            || lower.Contains("could not connect", StringComparison.Ordinal)
            || lower.Contains("server not operational", StringComparison.Ordinal))
        {
            return AdDirectoryFailureKind.ConnectionFailed;
        }

        if (lower.Contains("cannot find", StringComparison.Ordinal)
            || lower.Contains("not found", StringComparison.Ordinal)
            || lower.Contains("does not exist", StringComparison.Ordinal)
            || lower.Contains("no such object", StringComparison.Ordinal))
        {
            return AdDirectoryFailureKind.NotFound;
        }

        if (lower.Contains("access is denied", StringComparison.Ordinal)
            || lower.Contains("access denied", StringComparison.Ordinal)
            || lower.Contains("insufficient", StringComparison.Ordinal)
            || lower.Contains("unauthorized", StringComparison.Ordinal))
        {
            return AdDirectoryFailureKind.InvalidRequest;
        }

        return AdDirectoryFailureKind.InvalidRequest;
    }
}
