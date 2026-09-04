using System.Diagnostics;
using System.Text;
using ITAdmin.HostAgent.Contracts;

namespace ITAdmin.HostAgent;

/// <summary>
/// Reads the state of the source clone under <c>&lt;InstallRoot&gt;\src</c> against the public
/// repository.
///
/// <para>
/// The repository is public, so there is no key and no SSH: Git is invoked over anonymous HTTPS
/// with an explicit argument list (never a shell string) and every argument is a constant or a
/// branch name the agent read from its own configuration. There is no path from a caller's input to
/// a Git argument.
/// </para>
///
/// <para>
/// This component never mutates the working tree. Fetch updates remote-tracking refs only;
/// checking out the new tip, cleaning, building, and deploying are done by
/// <c>Deploy-ITAdmin.ps1</c>, which the Update Coordinator runs.
/// </para>
/// </summary>
public sealed class GitSourceClient(HostAgentSettings settings, string gitExecutable = "git")
{
    private const char UnitSeparator = '\u001f';

    /// <summary>Confirms the configured branch is reachable on the remote. Read-only.</summary>
    public async Task<RepositoryAccessDiagnosis> DiagnoseAccessAsync(CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            ["ls-remote", "--heads", settings.RepositoryUrl, settings.Branch],
            workingDirectory: null,
            cancellationToken);

        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return new RepositoryAccessDiagnosis(true, HostAgentRepositoryStatus.Verified, "Repository access verified.");
        }

        if (result.ExitCode == 0)
        {
            return new RepositoryAccessDiagnosis(
                false,
                HostAgentRepositoryStatus.RepositoryRejected,
                $"Branch '{settings.Branch}' was not found on the repository.");
        }

        return new RepositoryAccessDiagnosis(false, ClassifyRemoteFailure(result), DescribeRemoteFailure(result));
    }

    /// <summary>
    /// Fetches the configured branch and reports how far the deployed build (the current
    /// <c>HEAD</c> of the working tree) is behind its tip.
    /// </summary>
    public async Task<HostAgentUpdateAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        RequireSourceClone();

        var fetch = await RunGitAsync(["fetch", "--prune", "origin", settings.Branch], settings.SourceRoot, cancellationToken);
        if (fetch.ExitCode != 0)
        {
            throw new InvalidOperationException(DescribeRemoteFailure(fetch));
        }

        var current = (await RunGitOrThrowAsync(["rev-parse", "--short", "HEAD"], settings.SourceRoot, cancellationToken)).Trim();
        var latestLine = (await RunGitOrThrowAsync(
            ["log", "-1", $"--pretty=%h{UnitSeparator}%s", $"origin/{settings.Branch}"],
            settings.SourceRoot,
            cancellationToken)).Trim();
        var latestParts = latestLine.Split(UnitSeparator, 2);
        var latest = latestParts.Length > 0 ? latestParts[0].Trim() : string.Empty;
        var latestSubject = latestParts.Length > 1 ? latestParts[1].Trim() : string.Empty;

        var behindText = (await RunGitOrThrowAsync(
            ["rev-list", "--count", $"HEAD..origin/{settings.Branch}"],
            settings.SourceRoot,
            cancellationToken)).Trim();
        _ = int.TryParse(behindText, out var behind);

        return new HostAgentUpdateAvailability
        {
            Branch = settings.Branch,
            CurrentCommit = current,
            LatestCommit = latest,
            LatestSubject = latestSubject,
            CommitsBehind = behind,
            UpToDate = behind == 0,
        };
    }

    private void RequireSourceClone()
    {
        if (!Directory.Exists(Path.Combine(settings.SourceRoot, ".git")))
        {
            throw new InvalidOperationException(
                $"No source clone was found at {settings.SourceRoot}. Run Deploy-ITAdmin.ps1 on this host first.");
        }
    }

    private static string DescribeRemoteFailure(ProcessResult result)
    {
        var stderr = result.StandardError;

        if (stderr.Contains("Could not resolve host", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Connection timed out", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase))
        {
            return "The repository host could not be reached. Check outbound HTTPS connectivity and name resolution from this server.";
        }

        if (stderr.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Repository not found", StringComparison.OrdinalIgnoreCase))
        {
            return "The repository could not be read. Confirm the repositoryUrl in hostagent.json is correct and the repository is public.";
        }

        return $"Repository access failed (git exit {result.ExitCode}).";
    }

    private static HostAgentRepositoryStatus ClassifyRemoteFailure(ProcessResult result)
    {
        var stderr = result.StandardError;
        if (stderr.Contains("Could not resolve host", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Connection timed out", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase))
        {
            return HostAgentRepositoryStatus.HostUnreachable;
        }

        return HostAgentRepositoryStatus.RepositoryRejected;
    }

    private async Task<string> RunGitOrThrowAsync(
        string[] arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(arguments, workingDirectory, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(DescribeRemoteFailure(result));
        }

        return result.StandardOutput;
    }

    private async Task<ProcessResult> RunGitAsync(
        string[] arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = gitExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        // Any prompt from Git would hang a service with no console forever.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = startInfo };
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { standardOutput.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { standardError.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

public sealed record RepositoryAccessDiagnosis(
    bool IsAccessible,
    HostAgentRepositoryStatus Status,
    string Message);
