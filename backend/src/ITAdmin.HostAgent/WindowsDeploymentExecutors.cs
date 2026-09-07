using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ITAdmin.HostAgent;

/// <summary>
/// Applies an update by handing off to the one-shot ITAdmin Update Coordinator, and performs the
/// one narrow IIS operation the agent exposes directly.
///
/// <para>
/// The agent could run <c>Deploy-ITAdmin.ps1</c> itself. It deliberately does not: that script
/// stops and repoints the <c>ITAdminHostAgent</c> service when the Host Agent binary changed, and a
/// process cannot stop and replace itself. So the agent configures the Coordinator service to the
/// newest coordinator build and starts it; the Coordinator - a separate, short-lived LocalSystem
/// process - runs the deployment script and then swaps the Host Agent service.
/// </para>
///
/// <para>
/// Every argument the Coordinator receives is a service image path the agent built from its own
/// configuration. Nothing from the pipe reaches a command line: the web application cannot name a
/// script, a path, a branch, or a flag.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHostDeploymentExecutor(
    HostAgentSettings settings,
    ILogger<WindowsHostDeploymentExecutor> logger) : IHostDeploymentExecutor
{
    public const string CoordinatorServiceName = "ITAdminUpdateCoordinator";

    private static readonly string AppCmdPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "inetsrv", "appcmd.exe");

    public async Task<ReleaseUpdateResult> ApplyUpdateAsync(string operationId, CancellationToken cancellationToken)
    {
        var coordinatorExe = ResolveNewestCoordinatorExecutable();
        if (coordinatorExe is null)
        {
            return new ReleaseUpdateResult(false, "No Update Coordinator build was found. Run Deploy-ITAdmin.ps1 on this host first.");
        }

        // Only the operation id travels on the command line: the coordinator reads the ProgramData
        // root from HKLM\SOFTWARE\ITAdmin like the Host Agent does. Keeping the ImagePath to one
        // quoted path plus a short hex token avoids the sc.exe/argv quoting traps that left the
        // service registered but unstartable.
        var imagePath = $"\"{coordinatorExe}\" --operation-id {operationId}";
        var query = await RunAsync("sc.exe", ["query", CoordinatorServiceName], cancellationToken);
        var configure = query.ExitCode == 0
            ? await RunAsync("sc.exe", ["config", CoordinatorServiceName, "binPath=", imagePath, "start=", "demand"], cancellationToken)
            : await RunAsync("sc.exe", ["create", CoordinatorServiceName, "binPath=", imagePath, "start=", "demand", "obj=", "LocalSystem", "DisplayName=", "ITAdmin Update Coordinator"], cancellationToken);
        if (configure.ExitCode != 0)
        {
            logger.LogError("Could not configure {Service}: sc.exe exit {ExitCode}. {Output}", CoordinatorServiceName, configure.ExitCode, configure.Output);
            return new ReleaseUpdateResult(false, $"The Update Coordinator service could not be configured (sc.exe exit {configure.ExitCode}).");
        }

        var start = await RunAsync("sc.exe", ["start", CoordinatorServiceName], cancellationToken);

        // 0 = started; 1056 = already running (a prior handoff still finishing) - both are fine.
        if (start.ExitCode is 0 or 1056)
        {
            return new ReleaseUpdateResult(true, "The update was handed to the Update Coordinator.");
        }

        var state = await RunAsync("sc.exe", ["query", CoordinatorServiceName], cancellationToken);
        logger.LogError(
            "Could not start {Service}: sc.exe start exit {ExitCode} ({StartOutput}). Current state: {StateOutput}",
            CoordinatorServiceName, start.ExitCode, Tail(start.Output), Tail(state.Output));

        if (state.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)
            || state.Output.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase))
        {
            return new ReleaseUpdateResult(true, "The update was handed to the Update Coordinator.");
        }

        return new ReleaseUpdateResult(
            false,
            $"The Update Coordinator service could not be started (sc.exe exit {start.ExitCode}). "
            + "See %ProgramData%\\ITAdmin\\logs\\update-coordinator-startup.log and the Windows event log.");
    }

    public async Task<ReleaseUpdateResult> RecycleAppPoolAsync(string appPoolName, CancellationToken cancellationToken)
    {
        if (!File.Exists(AppCmdPath))
        {
            return new ReleaseUpdateResult(false, "IIS management tooling is not available on this host.");
        }

        var result = await RunAppCmdAsync(["recycle", "apppool", $"/apppool.name:{appPoolName}"], cancellationToken);
        return result.ExitCode == 0
            ? new ReleaseUpdateResult(true, "Application pool recycled.")
            : new ReleaseUpdateResult(false, "The application pool could not be recycled.");
    }

    public async Task<ReleaseUpdateResult> RunDeployScriptAsync(
        IReadOnlyList<string> extraArguments,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        var deployScript = settings.DeployScriptPath;
        if (!File.Exists(deployScript))
        {
            return new ReleaseUpdateResult(false, $"Deployment script not found at {deployScript}. Run Deploy-ITAdmin.ps1 on this host first.");
        }

        var arguments = new List<string>
        {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", deployScript,
            "-RepositoryUrl", settings.RepositoryUrl,
            "-Branch", settings.Branch,
            "-InstallRoot", settings.InstallRoot,
            "-DataRoot", settings.DataRoot,
        };
        arguments.AddRange(extraArguments);
        arguments.Add("-Unattended");

        var result = await RunAsync("powershell.exe", arguments, cancellationToken, environment);
        if (result.ExitCode == 0)
        {
            return new ReleaseUpdateResult(true, "The deployment script completed.");
        }

        logger.LogError("Deploy-ITAdmin.ps1 exited {ExitCode}. {Output}", result.ExitCode, Tail(result.Output));
        return new ReleaseUpdateResult(false, $"The deployment script exited {result.ExitCode}. See the ITAdmin Host Agent log.");
    }

    private static string Tail(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('\n', lines[^Math.Min(20, lines.Length)..]);
    }

    private string? ResolveNewestCoordinatorExecutable()
    {
        if (!Directory.Exists(settings.CoordinatorBuildsRoot))
        {
            return null;
        }

        return Directory.GetDirectories(settings.CoordinatorBuildsRoot)
            .Select(dir => Path.Combine(dir, "ITAdmin.UpdateCoordinator.exe"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static async Task<(int ExitCode, string Output)> RunAppCmdAsync(string[] arguments, CancellationToken cancellationToken) =>
        await RunAsync(AppCmdPath, arguments, cancellationToken);

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
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

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); } };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, output.ToString());
    }
}
