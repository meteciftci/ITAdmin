using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ITAdmin.HostAgent;

/// <summary>
/// Hands a verified release to the one-shot update coordinator.
///
/// <para>
/// The agent could reimplement staging, migration, and activation in C#. It deliberately does not.
/// Those steps carry the hard-won details - stage-then-verify-then-move, phase recorded before the
/// work, migration-in-flight marking, physicalPath activation, health gate, fail-closed state - and
/// a parallel implementation would drift from the one that first installs the machine. So the
/// update path runs the same script the operator ran, on an already-verified local directory.
/// </para>
///
/// <para>
/// The critical property is that <em>every</em> argument below is constructed here from values the
/// agent itself derived: the release directory it fetched and verified, and the layout roots from
/// its own configuration. Nothing from the pipe reaches this command line. The web application
/// cannot name a script, a path, or a flag.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InstallerReleaseUpdateExecutor(
    HostAgentSettings settings,
    ILogger<InstallerReleaseUpdateExecutor> logger) : IReleaseUpdateExecutor
{
    public async Task<ReleaseUpdateResult> ApplyAsync(
        ReleaseUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var coordinatorPath = Path.Combine(
            request.VerifiedReleaseDirectory,
            ITAdmin.Deployment.DeploymentLayout.UpdateCoordinatorDirectoryName,
            "ITAdmin.UpdateCoordinator.exe");
        if (!File.Exists(coordinatorPath))
        {
            return new ReleaseUpdateResult(
                false,
                "The verified release does not contain the update coordinator.");
        }

        var statePath = new ITAdmin.Deployment.DeploymentLayout(
            settings.ProgramFilesRoot,
            settings.ProgramDataRoot).InstallationStatePath;
        var state = ITAdmin.Deployment.InstallationState.FromJson(await File.ReadAllTextAsync(statePath, cancellationToken));
        var operationId = state?.CurrentOperation?.Id;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return new ReleaseUpdateResult(false, "The durable update operation could not be read.");
        }

        var service = "ITAdminUpdateCoordinator";
        var imagePath = $"\"{coordinatorPath}\" --operation-id {operationId}";
        var query = await RunAsync("sc.exe", ["query", service], cancellationToken);
        var configure = query.ExitCode == 0
            ? await RunAsync("sc.exe", ["config", service, "binPath=", imagePath, "start=", "demand"], cancellationToken)
            : await RunAsync("sc.exe", ["create", service, "binPath=", imagePath, "start=", "demand", "obj=", "LocalSystem", "DisplayName=", "ITAdmin Update Coordinator"], cancellationToken);
        if (configure.ExitCode != 0)
        {
            return new ReleaseUpdateResult(false, "The Update Coordinator service could not be configured.");
        }

        var result = await RunAsync("sc.exe", ["start", service], cancellationToken);

        if (result.ExitCode == 0)
        {
            return new ReleaseUpdateResult(true, $"ITAdmin {request.Version} was handed to the update coordinator.");
        }

        logger.LogError(
            "Update coordinator exited {ExitCode} while applying {Version}. Output: {Output}",
            result.ExitCode,
            request.Version,
            result.Output);

        return new ReleaseUpdateResult(
            false,
            $"Applying release {request.Version} failed. See the ITAdmin Host Agent log and the "
            + "installation state file.");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, output.ToString());
    }
}

/// <summary>
/// Applies host name / HTTPS / redirect settings to the IIS site, and recycles the app pool.
///
/// <para>
/// This is where the deferred HTTPS work lands. An administrator chooses a host name and a
/// certificate in ITAdmin Settings; the web application forwards that as a typed intent; this class
/// - running as LocalSystem, in a separate process, with no request context anywhere near it -
/// makes the IIS change. The application never gains the ability to write applicationHost.config,
/// which is the whole reason the boundary exists.
/// </para>
///
/// <para>
/// The IIS mutation itself is performed through <c>appcmd.exe</c> in the concrete steps below and
/// requires a Windows host with IIS to exercise; it is marked as an acceptance item rather than
/// claimed as verified.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class IisWebBindingReconciler(
    ILogger<IisWebBindingReconciler> logger) : IWebBindingReconciler
{
    private static readonly string AppCmdPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "system32",
        "inetsrv",
        "appcmd.exe");

    public async Task<ReleaseUpdateResult> ReconcileAsync(
        WebBindingIntent intent,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(AppCmdPath))
        {
            return new ReleaseUpdateResult(false, "IIS management tooling is not available on this host.");
        }

        // The HTTP binding always stays. Removing it before a redirect is in place is how an
        // administrator locks themselves out of a server whose certificate turns out to be wrong.
        var host = intent.HostName ?? string.Empty;
        var steps = new List<string[]>();
        steps.Add(
        [
            "set", "site", $"/site.name:{intent.SiteName}",
            $"/+bindings.[protocol='http',bindingInformation='*:80:{host}']",
        ]);

        if (intent.EnableHttps)
        {
            steps.Add([
                "set", "site", $"/site.name:{intent.SiteName}",
                $"/+bindings.[protocol='https',bindingInformation='*:443:{host}',sslFlags='1']",
            ]);
        }

        foreach (var step in steps)
        {
            var exitCode = await RunAppCmdAsync(step, cancellationToken);

            // appcmd returns ERROR_ALREADY_EXISTS (183) when the binding is already present, which
            // is the normal outcome of re-running reconciliation and is not a failure.
            if (exitCode is not (0 or 183))
            {
                logger.LogError("appcmd exited {ExitCode} while reconciling bindings.", exitCode);
                return new ReleaseUpdateResult(false, "IIS binding reconciliation failed on the host.");
            }
        }

        return new ReleaseUpdateResult(true, "Web bindings reconciled.");
    }

    public async Task<ReleaseUpdateResult> RecycleApplicationPoolAsync(
        string appPoolName,
        CancellationToken cancellationToken)
    {
        var exitCode = await RunAppCmdAsync(["recycle", "apppool", $"/apppool.name:{appPoolName}"], cancellationToken);

        return exitCode == 0
            ? new ReleaseUpdateResult(true, "Application pool recycled.")
            : new ReleaseUpdateResult(false, "The application pool could not be recycled.");
    }

    private static async Task<int> RunAppCmdAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = AppCmdPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
