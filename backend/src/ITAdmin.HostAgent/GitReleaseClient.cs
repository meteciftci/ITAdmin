using System.Diagnostics;
using System.Text;
using ITAdmin.Deployment;

namespace ITAdmin.HostAgent;

/// <summary>
/// Talks to the ITAdmin repository using the machine's read-only deploy key.
///
/// <para>
/// This is the only component on the host that touches the key, and it never reads its bytes: it
/// points <c>GIT_SSH_COMMAND</c> at the key file and lets OpenSSH do the reading, so the key is
/// never in this process's memory, its logs, or a crash dump. <c>IdentitiesOnly=yes</c> stops SSH
/// from silently trying an agent identity or a user key instead, which would make an installation
/// depend on whichever account happened to run it.
/// </para>
///
/// <para>
/// Git is invoked with an explicit argument list, never a shell string, and every argument is
/// either a constant or a ref name the agent constructed itself from a parsed version. There is no
/// path from a caller's input to a Git argument.
/// </para>
/// </summary>
public sealed class GitReleaseClient(HostAgentSettings settings, string gitExecutable = "git")
{
    /// <summary>
    /// Refs advertised by the remote, as raw <c>ls-remote</c> lines for
    /// <see cref="ReleaseTagResolver"/> to interpret.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListRemoteTagsAsync(CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            ["ls-remote", "--tags", settings.RepositoryUrl],
            workingDirectory: null,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(DescribeRemoteFailure(result));
        }

        return result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Fetches one distribution ref into <paramref name="destinationDirectory"/> and checks out its
    /// tree.
    ///
    /// <para>
    /// Depth 1 on a ref whose commit is an orphan means Git transfers exactly one commit and one
    /// tree: no source history, no previous releases, no tags. That is what makes a Git-delivered
    /// binary payload practical - the server downloads the release it is installing and nothing
    /// else, however long the repository's history becomes.
    /// </para>
    /// </summary>
    public async Task FetchDistributionAsync(
        ReleaseVersion version,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        Directory.CreateDirectory(destinationDirectory);

        var distributionRef = GitReleaseRefs.DistributionRef(version);

        await RunGitOrThrowAsync(["init", "--quiet"], destinationDirectory, cancellationToken);
        await RunGitOrThrowAsync(
            ["remote", "add", "origin", settings.RepositoryUrl],
            destinationDirectory,
            cancellationToken);
        await RunGitOrThrowAsync(
            ["fetch", "--depth", "1", "--quiet", "origin", distributionRef],
            destinationDirectory,
            cancellationToken);
        await RunGitOrThrowAsync(["checkout", "--quiet", "FETCH_HEAD"], destinationDirectory, cancellationToken);
    }

    /// <summary>
    /// Diagnoses repository access without fetching anything, so a broken deploy key is reported as
    /// a key problem rather than surfacing later as a mysterious update failure.
    /// </summary>
    public async Task<RepositoryAccessDiagnosis> DiagnoseAccessAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.DeployKeyPath))
        {
            return new RepositoryAccessDiagnosis(
                false,
                $"The deploy key is missing at {settings.DeployKeyPath}. Re-run the ITAdmin bootstrap "
                + "to reinstate repository access.");
        }

        if (!File.Exists(settings.KnownHostsPath))
        {
            // Without machine-owned host keys the agent would either refuse every connection or,
            // if strict checking were relaxed, trust whatever answered - so this is reported as its
            // own fault rather than surfacing later as an opaque transport error.
            return new RepositoryAccessDiagnosis(
                false,
                $"The machine known-hosts file is missing at {settings.KnownHostsPath}. Re-run the "
                + "ITAdmin bootstrap so the verified repository host key is persisted for the service.");
        }

        var result = await RunGitAsync(
            ["ls-remote", "--tags", "--quiet", settings.RepositoryUrl],
            workingDirectory: null,
            cancellationToken);

        return result.ExitCode == 0
            ? new RepositoryAccessDiagnosis(true, "Repository access verified.")
            : new RepositoryAccessDiagnosis(false, DescribeRemoteFailure(result));
    }

    /// <summary>
    /// Turns Git/SSH transport failure into something an operator can act on. The three cases below
    /// look identical in a raw stderr dump but have completely different fixes.
    /// </summary>
    private static string DescribeRemoteFailure(ProcessResult result)
    {
        var stderr = result.StandardError;

        if (stderr.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("publickey", StringComparison.OrdinalIgnoreCase))
        {
            return "The repository refused the deploy key. Confirm the key is still listed as a "
                + "Deploy Key on the ITAdmin repository and has not been revoked.";
        }

        if (stderr.Contains("Could not resolve hostname", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Connection timed out", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase))
        {
            return "The repository host could not be reached. Check outbound SSH connectivity and "
                + "name resolution from this server.";
        }

        if (stderr.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase))
        {
            return "The repository host key is not trusted by this machine. Add it to the machine "
                + "known_hosts file used by the ITAdmin Host Agent.";
        }

        return $"Repository access failed (git exit {result.ExitCode}).";
    }

    private async Task RunGitOrThrowAsync(
        string[] arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(arguments, workingDirectory, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(DescribeRemoteFailure(result));
        }
    }

    private async Task<ProcessResult> RunGitAsync(
        string[] arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
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

        startInfo.Environment["GIT_SSH_COMMAND"] =
            RepositoryAccessContract.BuildSshCommand(settings.DeployKeyPath, settings.KnownHostsPath);
        // Any prompt from Git or SSH would hang a service with no console forever.
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["SSH_ASKPASS"] = string.Empty;

        using var process = new Process { StartInfo = startInfo };
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                standardOutput.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                standardError.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

public sealed record RepositoryAccessDiagnosis(bool IsAccessible, string Message);
