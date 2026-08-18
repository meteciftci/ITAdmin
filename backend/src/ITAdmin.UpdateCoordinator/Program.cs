using System.Diagnostics;
using System.Text.Json;
using ITAdmin.Deployment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Win32;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The ITAdmin Update Coordinator runs on Windows only.");
    return 2;
}

if (args.Length != 2 || args[0] != "--operation-id" || !CoordinatorRunner.IsOperationId(args[1]))
{
    Console.Error.WriteLine("Usage: ITAdmin.UpdateCoordinator --operation-id <32-hex-id>");
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "ITAdminUpdateCoordinator");
builder.Services.AddSingleton(new CoordinatorRequest(args[1].ToLowerInvariant()));
builder.Services.AddHostedService<CoordinatorWorker>();
await builder.Build().RunAsync();
return Environment.ExitCode;

internal sealed record CoordinatorRequest(string OperationId);

internal sealed class CoordinatorWorker(CoordinatorRequest request, IHostApplicationLifetime lifetime)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Environment.ExitCode = await CoordinatorRunner.RunAsync(request.OperationId, stoppingToken);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}

internal static class CoordinatorRunner
{
    internal static bool IsOperationId(string value) =>
        value.Length == 32 && value.All(Uri.IsHexDigit);

    internal static async Task<int> RunAsync(string operationId, CancellationToken cancellationToken)
    {
        DeploymentLayout? layout = null;
        DeploymentOperation? operation = null;

        try
        {
#pragma warning disable CA1416 // Reached only after the Windows guard at process entry.
            var programDataRoot = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\ITAdmin",
                "ProgramDataRoot",
                DeploymentLayout.DefaultProgramDataRoot) as string ?? DeploymentLayout.DefaultProgramDataRoot;
#pragma warning restore CA1416
            var settings = JsonSerializer.Deserialize<CoordinatorHostSettings>(
                await File.ReadAllTextAsync(Path.Combine(programDataRoot, "config", "hostagent.json"), cancellationToken),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Host Agent configuration could not be read.");
            layout = new DeploymentLayout(settings.ProgramFilesRoot, programDataRoot);
            var state = ReadState(layout);
            operation = state.CurrentOperation;
            if (operation is null
                || !string.Equals(operation.Id, operationId, StringComparison.Ordinal)
                || operation.Kind is not DeploymentOperationKind.Update
                || string.IsNullOrWhiteSpace(operation.VerifiedReleaseDirectory)
                || string.IsNullOrWhiteSpace(operation.ExpectedSourceCommit)
                || string.IsNullOrWhiteSpace(operation.TargetVersion))
            {
                return 3;
            }

            var releaseRoot = operation.VerifiedReleaseDirectory;
            var manifest = ReadManifest(releaseRoot);
            var acquisition = ReleaseAcquisition.Verify(
                releaseRoot,
                ReleaseVersion.Parse(operation.TargetVersion),
                operation.ExpectedSourceCommit);
            if (!acquisition.IsAcceptable)
            {
                WriteOperation(layout, operation.Advance(
                    DeploymentOperationStage.Failed,
                    "Release verification failed in the update coordinator.",
                    DateTimeOffset.UtcNow));
                return 4;
            }

            WriteOperation(layout, operation.Advance(
                DeploymentOperationStage.Staging,
                "The verified release is being staged.",
                DateTimeOffset.UtcNow));

            var installerResult = await RunProcessAsync("powershell.exe",
            [
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", Path.Combine(releaseRoot, DeploymentLayout.DeploymentToolingDirectoryName, "Install-ITAdmin.ps1"),
                "-ReleaseDirectory", releaseRoot,
                "-ExpectedVersion", manifest.Source.Version,
                "-ExpectedSourceCommit", manifest.Source.Commit,
                "-ProgramFilesRoot", settings.ProgramFilesRoot,
                "-ProgramDataRoot", programDataRoot,
                "-SiteName", settings.SiteName,
                "-AppPoolName", settings.AppPoolName,
                "-Unattended",
            ], cancellationToken);

            if (installerResult != 0)
            {
                var failedState = ReadState(layout);
                var unsafeFailure = failedState.MigrationInFlight
                    || failedState.LastError?.Step is "Migrate" or "Activate" or "HealthCheck";
                WriteOperation(layout, operation.Advance(
                    unsafeFailure ? DeploymentOperationStage.RequiresOperatorReview : DeploymentOperationStage.Failed,
                    unsafeFailure
                        ? "The update stopped after database or live-site changes; operator review is required."
                        : "The update failed before activation. See the ITAdmin deployment logs.",
                    DateTimeOffset.UtcNow));
                return installerResult;
            }

            var hostAgentExecutable = StageHostAgent(releaseRoot, manifest.Source.Version, settings.ProgramFilesRoot);
            WriteOperation(layout, operation.Advance(
                DeploymentOperationStage.Completed,
                $"ITAdmin {manifest.Source.Version} is active.",
                DateTimeOffset.UtcNow));
            if (hostAgentExecutable is not null)
            {
                await RestartHostAgentAsync(hostAgentExecutable, cancellationToken);
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Update coordinator failed: {exception.Message}");
            if (layout is not null && operation is not null && !operation.IsTerminal)
            {
                try
                {
                    var current = ReadState(layout);
                    var unsafeFailure = current.MigrationInFlight
                        || current.LastError?.Step is "Migrate" or "Activate" or "HealthCheck";
                    WriteOperation(layout, operation.Advance(
                        unsafeFailure
                            ? DeploymentOperationStage.RequiresOperatorReview
                            : DeploymentOperationStage.Failed,
                        unsafeFailure
                            ? "The coordinator stopped after database or live-site changes; operator review is required."
                            : "The update coordinator failed. See the host log for details.",
                        DateTimeOffset.UtcNow));
                }
                catch (Exception stateException)
                {
                    Console.Error.WriteLine($"Update failure state could not be persisted: {stateException.Message}");
                }
            }
            return 1;
        }
    }

    private static ReleaseManifest ReadManifest(string root) =>
        ReleaseManifest.FromJson(File.ReadAllText(Path.Combine(root, ReleaseManifest.FileName)))
        ?? throw new InvalidDataException("The verified release manifest could not be read.");

    private static InstallationState ReadState(DeploymentLayout layout) =>
        InstallationState.FromJson(File.ReadAllText(layout.InstallationStatePath))
        ?? throw new InvalidDataException("Installation state could not be read.");

    private static void WriteOperation(DeploymentLayout layout, DeploymentOperation operation)
    {
        var state = ReadState(layout) with { CurrentOperation = operation, UpdatedAtUtc = DateTimeOffset.UtcNow };
        var temporary = layout.InstallationStatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, state.ToJson());
        File.Move(temporary, layout.InstallationStatePath, overwrite: true);
    }

    private static async Task<int> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo { FileName = fileName, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Process could not be started.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    private static string? StageHostAgent(string releaseRoot, string version, string programFilesRoot)
    {
        var source = Path.Combine(releaseRoot, DeploymentLayout.HostAgentDirectoryName);
        if (!Directory.Exists(source)) return null;
        var target = Path.Combine(programFilesRoot, "hostagent", "releases", version);
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: true);
        var executable = Path.Combine(target, "ITAdmin.HostAgent.exe");
        return File.Exists(executable)
            ? executable
            : throw new FileNotFoundException("The target Host Agent executable is missing.");
    }

    private static async Task RestartHostAgentAsync(string executable, CancellationToken cancellationToken)
    {
        var configured = await RunProcessAsync(
            "sc.exe",
            ["config", "ITAdminHostAgent", "binPath=", $"\"{executable}\""],
            cancellationToken);
        if (configured != 0) throw new InvalidOperationException("The Host Agent service image path could not be updated.");
        await RunProcessAsync("sc.exe", ["stop", "ITAdminHostAgent"], cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        var started = await RunProcessAsync("sc.exe", ["start", "ITAdminHostAgent"], cancellationToken);
        if (started != 0) throw new InvalidOperationException("The updated Host Agent service could not be started.");
    }
}

internal sealed record CoordinatorHostSettings
{
    public string ProgramFilesRoot { get; init; } = DeploymentLayout.DefaultProgramFilesRoot;
    public string SiteName { get; init; } = "ITAdmin";
    public string AppPoolName { get; init; } = "ITAdmin";
}
