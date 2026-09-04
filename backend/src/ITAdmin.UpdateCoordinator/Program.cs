using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

// ITAdmin Update Coordinator: the one-shot handoff used when an in-app update needs to replace the
// release that contains the currently running ITAdmin Host Agent.
//
// The Host Agent cannot stop and repoint its own Windows service; a separate, short-lived
// LocalSystem process can. This runs Deploy-ITAdmin.ps1 - the checked-out deployment script, the
// same one an operator would run by hand - and then, only if the Host Agent binary actually
// changed, swaps the ITAdminHostAgent service to the new build.

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The ITAdmin Update Coordinator runs on Windows only.");
    return 2;
}

if (args.Length != 4 || args[0] != "--operation-id" || !CoordinatorRunner.IsOperationId(args[1])
    || args[2] != "--data-root" || string.IsNullOrWhiteSpace(args[3]))
{
    Console.Error.WriteLine("Usage: ITAdmin.UpdateCoordinator --operation-id <32-hex-id> --data-root <path>");
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "ITAdminUpdateCoordinator");
builder.Services.AddSingleton(new CoordinatorRequest(args[1].ToLowerInvariant(), args[3]));
builder.Services.AddHostedService<CoordinatorWorker>();
await builder.Build().RunAsync();
return Environment.ExitCode;

internal sealed record CoordinatorRequest(string OperationId, string DataRoot);

internal sealed class CoordinatorWorker(CoordinatorRequest request, IHostApplicationLifetime lifetime)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Environment.ExitCode = await CoordinatorRunner.RunAsync(request.OperationId, request.DataRoot, stoppingToken);
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}

internal static class CoordinatorRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static bool IsOperationId(string value) =>
        value.Length == 32 && value.All(Uri.IsHexDigit);

    internal static async Task<int> RunAsync(string operationId, string dataRoot, CancellationToken cancellationToken)
    {
        var operationPath = Path.Combine(dataRoot, "state", "update-operation.json");

        try
        {
            var settingsPath = Path.Combine(dataRoot, "config", "hostagent.json");
            var settings = JsonSerializer.Deserialize<CoordinatorHostSettings>(
                await File.ReadAllTextAsync(settingsPath, cancellationToken), JsonOptions)
                ?? throw new InvalidDataException("Host Agent configuration could not be read.");

            var operation = ReadOperation(operationPath);
            if (operation is null || !string.Equals(operation.OperationId, operationId, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"No matching update operation {operationId} was found at {operationPath}.");
                return 3;
            }

            WriteOperation(operationPath, operation with
            {
                Phase = "Building",
                Message = "Fetching the branch tip and building it on this host.",
            });

            var deployScript = Path.Combine(settings.InstallRoot, "src", "scripts", "deploy", "Deploy-ITAdmin.ps1");
            if (!File.Exists(deployScript))
            {
                WriteOperation(operationPath, operation with
                {
                    Phase = "Failed",
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Message = $"Deployment script not found at {deployScript}.",
                });
                return 4;
            }

            var previousHostAgentExecutable = ResolveNewestHostAgentExecutable(settings.InstallRoot);

            var exitCode = await RunProcessAsync("powershell.exe",
            [
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", deployScript,
                "-RepositoryUrl", settings.RepositoryUrl,
                "-Branch", settings.Branch,
                "-InstallRoot", settings.InstallRoot,
                "-DataRoot", dataRoot,
                "-Unattended",
                "-NoHostAgentService",
            ], cancellationToken);

            if (exitCode != 0)
            {
                WriteOperation(operationPath, operation with
                {
                    Phase = "Failed",
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Message = $"Deploy-ITAdmin.ps1 exited {exitCode}. See the deployment log for detail.",
                });
                return exitCode;
            }

            var newestHostAgentExecutable = ResolveNewestHostAgentExecutable(settings.InstallRoot);
            if (newestHostAgentExecutable is not null
                && !string.Equals(newestHostAgentExecutable, previousHostAgentExecutable, StringComparison.OrdinalIgnoreCase))
            {
                await SwapHostAgentServiceAsync(newestHostAgentExecutable, cancellationToken);
            }

            WriteOperation(operationPath, operation with
            {
                Phase = "Completed",
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Message = "The update was applied.",
            });
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Update coordinator failed: {exception.Message}");
            try
            {
                var operation = ReadOperation(operationPath);
                if (operation is not null)
                {
                    WriteOperation(operationPath, operation with
                    {
                        Phase = "Failed",
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                        Message = "The update coordinator failed. See the Windows Application/System event log for detail.",
                    });
                }
            }
            catch (Exception stateException)
            {
                Console.Error.WriteLine($"Update failure could not be recorded: {stateException.Message}");
            }

            return 1;
        }
    }

    private static string? ResolveNewestHostAgentExecutable(string installRoot)
    {
        var root = Path.Combine(installRoot, "hostagent");
        if (!Directory.Exists(root))
        {
            return null;
        }

        return Directory.GetDirectories(root)
            .Select(dir => Path.Combine(dir, "ITAdmin.HostAgent.exe"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static async Task SwapHostAgentServiceAsync(string executable, CancellationToken cancellationToken)
    {
        var configured = await RunProcessAsync(
            "sc.exe", ["config", "ITAdminHostAgent", "binPath=", $"\"{executable}\""], cancellationToken);
        if (configured != 0)
        {
            throw new InvalidOperationException("The Host Agent service image path could not be updated.");
        }

        await RunProcessAsync("sc.exe", ["stop", "ITAdminHostAgent"], cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        var started = await RunProcessAsync("sc.exe", ["start", "ITAdminHostAgent"], cancellationToken);
        if (started != 0)
        {
            throw new InvalidOperationException("The updated Host Agent service could not be started.");
        }
    }

    private static UpdateOperationRecord? ReadOperation(string path) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<UpdateOperationRecord>(File.ReadAllText(path), JsonOptions)
            : null;

    private static void WriteOperation(string path, UpdateOperationRecord operation)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(operation, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    private static async Task<int> RunProcessAsync(
        string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo { FileName = fileName, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Process could not be started.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}

/// <summary>The subset of <c>hostagent.json</c> the coordinator needs to invoke the deploy script.</summary>
internal sealed record CoordinatorHostSettings
{
    public string RepositoryUrl { get; init; } = string.Empty;
    public string Branch { get; init; } = "main";
    public string InstallRoot { get; init; } = @"C:\ITAdmin";
    public string SiteName { get; init; } = "ITAdmin";
    public string AppPoolName { get; init; } = "ITAdmin";
}

/// <summary>
/// Mirrors <c>ITAdmin.HostAgent.UpdateOperationRecord</c>. Duplicated rather than shared: the
/// coordinator is a separate, minimal-surface process by design and does not link the Host Agent
/// project.
/// </summary>
internal sealed record UpdateOperationRecord
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; init; } = string.Empty;

    [JsonPropertyName("phase")]
    public string Phase { get; init; } = "Idle";

    [JsonPropertyName("targetCommit")]
    public string? TargetCommit { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public DateTimeOffset? StartedAtUtc { get; init; }

    [JsonPropertyName("completedAtUtc")]
    public DateTimeOffset? CompletedAtUtc { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
