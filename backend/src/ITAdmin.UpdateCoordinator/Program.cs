using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Win32;

// ITAdmin Update Coordinator: the one-shot handoff used when an in-app update needs to replace the
// release that contains the currently running ITAdmin Host Agent.
//
// The Host Agent cannot stop and repoint its own Windows service; a separate, short-lived
// LocalSystem process can. This runs Deploy-ITAdmin.ps1 - the checked-out deployment script, the
// same one an operator would run by hand - and then, only if the Host Agent binary actually
// changed, swaps the ITAdminHostAgent service to the new build.

// Resolve the ProgramData root the way the Host Agent does, so a startup failure can always be
// written somewhere - "no log at all" has been the recurring diagnosis problem.
const string DefaultProgramDataRoot = @"C:\ProgramData\ITAdmin";
var programDataRoot = DefaultProgramDataRoot;
try
{
#pragma warning disable CA1416 // Guarded by the Windows check below in practice; Registry no-ops elsewhere.
    if (OperatingSystem.IsWindows())
    {
        programDataRoot = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\ITAdmin", "ProgramDataRoot", DefaultProgramDataRoot) as string
            ?? DefaultProgramDataRoot;
    }
#pragma warning restore CA1416
}
catch (Exception)
{
    programDataRoot = DefaultProgramDataRoot;
}

CoordinatorStartupLog.Initialize(programDataRoot);
CoordinatorStartupLog.Write($"starting; args=[{string.Join(' ', args)}]");

if (!OperatingSystem.IsWindows())
{
    CoordinatorStartupLog.Write("not Windows; exiting 2");
    return 2;
}

// Tolerant parse: --operation-id <32-hex> is the only required argument. --data-root is optional
// (it falls back to the registry-derived ProgramData root). Order and extra tokens are ignored,
// so a mangled service ImagePath cannot silently break the handoff.
string? operationId = null;
string? dataRootArg = null;
for (var i = 0; i < args.Length - 1; i++)
{
    if (string.Equals(args[i], "--operation-id", StringComparison.OrdinalIgnoreCase)) { operationId = args[i + 1]; }
    else if (string.Equals(args[i], "--data-root", StringComparison.OrdinalIgnoreCase)) { dataRootArg = args[i + 1]; }
}

if (operationId is null || !CoordinatorRunner.IsOperationId(operationId))
{
    CoordinatorStartupLog.Write($"missing/invalid --operation-id (got '{operationId ?? "<null>"}'); exiting 2");
    return 2;
}

var dataRoot = string.IsNullOrWhiteSpace(dataRootArg) ? programDataRoot : dataRootArg!;
CoordinatorStartupLog.Write($"operationId={operationId}; dataRoot={dataRoot}");

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "ITAdminUpdateCoordinator");
builder.Services.AddSingleton(new CoordinatorRequest(operationId.ToLowerInvariant(), dataRoot));
builder.Services.AddHostedService<CoordinatorWorker>();

try
{
    await builder.Build().RunAsync();
    CoordinatorStartupLog.Write($"host exited; ExitCode={Environment.ExitCode}");
}
catch (Exception exception)
{
    CoordinatorStartupLog.Write($"host threw: {exception}");
    return 1;
}

return Environment.ExitCode;

internal sealed record CoordinatorRequest(string OperationId, string DataRoot);

/// <summary>Append-only startup breadcrumb so a failed handoff is never silent.</summary>
internal static class CoordinatorStartupLog
{
    private static string? _path;

    public static void Initialize(string programDataRoot)
    {
        try
        {
            var directory = Path.Combine(programDataRoot, "logs");
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, "update-coordinator-startup.log");
        }
        catch (Exception)
        {
            _path = null;
        }
    }

    public static void Write(string message)
    {
        Console.Error.WriteLine(message);
        if (_path is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(_path, $"{DateTimeOffset.UtcNow:O}  {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // best effort
        }
    }
}

internal sealed class CoordinatorWorker(CoordinatorRequest request, IHostApplicationLifetime lifetime)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            CoordinatorStartupLog.Write("worker running the deployment");
            Environment.ExitCode = await CoordinatorRunner.RunAsync(request.OperationId, request.DataRoot, stoppingToken);
            CoordinatorStartupLog.Write($"worker finished; result={Environment.ExitCode}");
        }
        catch (Exception exception)
        {
            CoordinatorStartupLog.Write($"worker threw: {exception}");
            Environment.ExitCode = 1;
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
            if (operation is null)
            {
                // The Host Agent normally writes this before starting us. If it is missing (a lost
                // state write), synthesise one rather than stranding the update.
                CoordinatorStartupLog.Write($"no update-operation.json at {operationPath}; synthesising one");
                operation = new UpdateOperationRecord
                {
                    OperationId = operationId,
                    Phase = "Pulling",
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    Message = "Recovered by the Update Coordinator.",
                };
            }
            else if (!string.Equals(operation.OperationId, operationId, StringComparison.Ordinal))
            {
                // The Host Agent asked for this operation id; the file holds a different one. Proceed
                // anyway - a stale id must not strand the update - but record it.
                CoordinatorStartupLog.Write(
                    $"operation id mismatch (arg {operationId}, file {operation.OperationId}); proceeding with the arg id");
                operation = operation with { OperationId = operationId };
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

            var logDirectory = Path.Combine(dataRoot, "logs");
            Directory.CreateDirectory(logDirectory);
            var coordinatorLog = Path.Combine(
                logDirectory, $"update-coordinator-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

            var (exitCode, output) = await RunProcessAsync("powershell.exe",
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

            try { await File.WriteAllTextAsync(coordinatorLog, output, cancellationToken); }
            catch (Exception) { /* diagnostics only */ }

            if (exitCode != 0)
            {
                WriteOperation(operationPath, operation with
                {
                    Phase = "Failed",
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Message = $"Deploy-ITAdmin.ps1 exited {exitCode}. {LastLines(output, 12)} "
                        + $"Full output: {coordinatorLog}",
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
        if (configured.ExitCode != 0)
        {
            throw new InvalidOperationException("The Host Agent service image path could not be updated.");
        }

        await RunProcessAsync("sc.exe", ["stop", "ITAdminHostAgent"], cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        var started = await RunProcessAsync("sc.exe", ["start", "ITAdminHostAgent"], cancellationToken);
        if (started.ExitCode != 0)
        {
            throw new InvalidOperationException("The updated Host Agent service could not be started.");
        }
    }

    private static UpdateOperationRecord? ReadOperation(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        // Share-all so a concurrent status poll on the Host Agent side never blocks a replace.
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<UpdateOperationRecord>(stream, JsonOptions);
    }

    private static void WriteOperation(string path, UpdateOperationRecord operation)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        try
        {
            foreach (var stale in Directory.EnumerateFiles(directory, Path.GetFileName(path) + ".*.tmp"))
            {
                try { File.Delete(stale); } catch (Exception) { /* in use */ }
            }
        }
        catch (Exception) { /* best effort */ }

        var content = JsonSerializer.Serialize(operation, JsonOptions);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, content);
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    File.Move(temporary, path, overwrite: true);
                    return;
                }
                catch (Exception exception)
                    when ((exception is IOException or UnauthorizedAccessException) && attempt < 5)
                {
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception)
        {
            try { File.Copy(temporary, path, overwrite: true); }
            catch (Exception) { File.WriteAllText(path, content); }
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception) { /* pruned next write */ }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Process could not be started.");
        var buffer = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (buffer) { buffer.AppendLine(e.Data); } } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (buffer) { buffer.AppendLine(e.Data); } } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, buffer.ToString());
    }

    private static string LastLines(string text, int count)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Trim().Length > 0)
            .ToArray();
        return lines.Length == 0
            ? "(no output was captured)"
            : string.Join(" | ", lines[^Math.Min(count, lines.Length)..]);
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
