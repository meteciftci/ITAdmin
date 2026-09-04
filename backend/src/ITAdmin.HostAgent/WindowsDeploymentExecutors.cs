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

        var imagePath = $"\"{coordinatorExe}\" --operation-id {operationId} --data-root \"{settings.DataRoot}\"";
        var query = await RunAsync("sc.exe", ["query", CoordinatorServiceName], cancellationToken);
        var configure = query.ExitCode == 0
            ? await RunAsync("sc.exe", ["config", CoordinatorServiceName, "binPath=", imagePath, "start=", "demand"], cancellationToken)
            : await RunAsync("sc.exe", ["create", CoordinatorServiceName, "binPath=", imagePath, "start=", "demand", "obj=", "LocalSystem", "DisplayName=", "ITAdmin Update Coordinator"], cancellationToken);
        if (configure.ExitCode != 0)
        {
            logger.LogError("Could not configure {Service}: sc.exe exit {ExitCode}. {Output}", CoordinatorServiceName, configure.ExitCode, configure.Output);
            return new ReleaseUpdateResult(false, "The Update Coordinator service could not be configured.");
        }

        var start = await RunAsync("sc.exe", ["start", CoordinatorServiceName], cancellationToken);
        if (start.ExitCode == 0)
        {
            return new ReleaseUpdateResult(true, "The update was handed to the Update Coordinator.");
        }

        logger.LogError("Could not start {Service}: sc.exe exit {ExitCode}. {Output}", CoordinatorServiceName, start.ExitCode, start.Output);
        return new ReleaseUpdateResult(false, "The Update Coordinator service could not be started.");
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
        string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); } };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, output.ToString());
    }
}
