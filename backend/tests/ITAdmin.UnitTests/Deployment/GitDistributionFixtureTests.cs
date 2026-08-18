using System.Diagnostics;
using System.Text;
using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// Exercises the release mechanism against a real local Git repository.
///
/// <para>
/// The resolver's unit tests pin the rules; these pin the assumption underneath them - that
/// <c>git ls-remote --tags</c> really does emit a <c>^{}</c> row for an annotated tag and not for a
/// lightweight one, that a custom ref namespace can be pushed and fetched, and that a shallow fetch
/// of an orphan commit transfers one tree. Those are properties of Git, not of our code, and if any
/// of them were untrue the whole distribution design would be wrong in a way no amount of testing
/// our own parsing would reveal.
/// </para>
///
/// <para>
/// A local bare repository is used deliberately: it exercises the same plumbing as GitHub with no
/// network, no credentials, and nothing pushed anywhere real.
/// </para>
/// </summary>
public sealed class GitDistributionFixtureTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "itadmin-git-fixture-" + Guid.NewGuid().ToString("N"));

    private readonly string _bare;
    private readonly string _work;

    public GitDistributionFixtureTests()
    {
        _bare = Path.Combine(_root, "origin.git");
        _work = Path.Combine(_root, "work");

        Directory.CreateDirectory(_bare);
        Directory.CreateDirectory(_work);

        Git(_root, "init", "--bare", "--quiet", _bare);
        Git(_work, "init", "--quiet");
        Git(_work, "config", "user.name", "ITAdmin Test");
        Git(_work, "config", "user.email", "test@itadmin.invalid");
        Git(_work, "remote", "add", "origin", _bare);

        File.WriteAllText(Path.Combine(_work, "README.md"), "ITAdmin fixture");
        Git(_work, "add", "--all");
        Git(_work, "commit", "--quiet", "-m", "initial");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                // Git marks pack files read-only; clear that or the delete fails on Windows.
                foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void AnnotatedTag_AdvertisesAPeeledRow_LightweightDoesNot()
    {
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "tag", "v9.9.9");
        Git(_work, "push", "--quiet", "origin", "--tags");

        var lines = Git(_root, "ls-remote", "--tags", _bare)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Stable);

        Assert.True(resolution.IsResolved);
        Assert.Equal("1.0.0", resolution.Selected.Version.ToString());
        Assert.Contains(
            resolution.Rejected,
            rejection => rejection.TagName == "v9.9.9" && rejection.Reason == ReleaseTagRejection.Lightweight);
    }

    [Fact]
    public void PeeledCommit_MatchesTheCommitTheTagPointsAt()
    {
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");

        var expected = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();
        var lines = Git(_root, "ls-remote", "--tags", _bare).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var resolution = ReleaseTagResolver.Resolve(lines, ReleaseChannel.Stable);

        Assert.Equal(expected, resolution.Selected!.SourceCommit);
    }

    [Fact]
    public void DistributionRef_IsPushable_FetchableAtDepthOne_AndCarriesNoSourceHistory()
    {
        var version = ReleaseVersion.Parse("1.0.0");
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");
        Git(_work, "push", "--quiet", "origin", "HEAD:refs/heads/main");

        var sourceCommit = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();
        PublishDistribution(version, sourceCommit, payloadFileCount: 3);

        var consumer = Path.Combine(_root, "consumer");
        Directory.CreateDirectory(consumer);
        Git(consumer, "init", "--quiet");
        Git(consumer, "remote", "add", "origin", _bare);
        Git(consumer, "fetch", "--depth", "1", "--quiet", "origin", GitReleaseRefs.DistributionRef(version));
        Git(consumer, "checkout", "--quiet", "FETCH_HEAD");

        // The payload arrived...
        Assert.True(File.Exists(Path.Combine(consumer, ReleaseManifest.FileName)));
        Assert.True(Directory.Exists(Path.Combine(consumer, DeploymentLayout.PayloadDirectoryName)));

        // ...and nothing else did. No source tree, no branch, and exactly one commit: an orphan
        // distribution commit fetched at depth 1 cannot drag history along with it.
        Assert.False(File.Exists(Path.Combine(consumer, "README.md")));
        var history = Git(consumer, "rev-list", "--count", "HEAD").Trim();
        Assert.Equal("1", history);
    }

    [Fact]
    public void AcquiredRelease_VerifiesAgainstTheAnnotatedTagsIdentity()
    {
        var version = ReleaseVersion.Parse("1.0.0");
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");

        var sourceCommit = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();
        PublishDistribution(version, sourceCommit, payloadFileCount: 4);

        var acquired = FetchDistribution(version);

        var result = ReleaseAcquisition.Verify(acquired, version, sourceCommit);

        Assert.True(result.IsAcceptable, string.Join("; ", result.Problems));
        Assert.Equal("1.0.0", result.Manifest!.Source.Version);
    }

    [Fact]
    public void AcquiredRelease_BuiltFromADifferentCommit_IsRefused()
    {
        // The scenario this exists for: a distribution ref that carries a real, internally
        // consistent ITAdmin payload which simply is not the one the release tag names.
        var version = ReleaseVersion.Parse("1.0.0");
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");

        var taggedCommit = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();

        File.WriteAllText(Path.Combine(_work, "CHANGED.md"), "later work");
        Git(_work, "add", "--all");
        Git(_work, "commit", "--quiet", "-m", "later");
        var otherCommit = Git(_work, "rev-parse", "HEAD").Trim();

        PublishDistribution(version, otherCommit, payloadFileCount: 3);
        var acquired = FetchDistribution(version);

        var result = ReleaseAcquisition.Verify(acquired, version, taggedCommit);

        Assert.False(result.IsAcceptable);
        Assert.Equal(DistributionFault.SourceIdentityMismatch, result.Fault);
        Assert.Contains(result.Problems, problem => problem.Contains("source commit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AcquiredRelease_CarryingAnotherVersionsPayload_IsRefused()
    {
        var requested = ReleaseVersion.Parse("2.0.0");
        Git(_work, "tag", "-a", "v2.0.0", "-m", "ITAdmin 2.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");
        var sourceCommit = Git(_work, "rev-parse", "refs/tags/v2.0.0^{commit}").Trim();

        // Payload says 1.9.0 while sitting on the 2.0.0 distribution ref.
        PublishDistribution(requested, sourceCommit, payloadFileCount: 3, manifestVersionOverride: "1.9.0");
        var acquired = FetchDistribution(requested);

        var result = ReleaseAcquisition.Verify(acquired, requested, sourceCommit);

        Assert.False(result.IsAcceptable);
        Assert.Equal(DistributionFault.SourceIdentityMismatch, result.Fault);
        Assert.Contains(
            result.Problems,
            problem => problem.Contains("declares source version", StringComparison.Ordinal));
    }

    [Fact]
    public void AcquiredRelease_WithATamperedPayloadFile_IsRefused()
    {
        var version = ReleaseVersion.Parse("1.0.0");
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");
        var sourceCommit = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();

        PublishDistribution(version, sourceCommit, payloadFileCount: 3);
        var acquired = FetchDistribution(version);

        var payloadFile = Directory
            .EnumerateFiles(Path.Combine(acquired, DeploymentLayout.PayloadDirectoryName), "*", SearchOption.AllDirectories)
            .First();
        File.AppendAllText(payloadFile, "tampered");

        var result = ReleaseAcquisition.Verify(acquired, version, sourceCommit);

        Assert.False(result.IsAcceptable);
        Assert.Equal(DistributionFault.IntegrityFailure, result.Fault);
        // Component-qualified so a failure names which tree it came from.
        Assert.Contains(result.Problems, problem => problem.Contains("altered:", StringComparison.Ordinal));
    }

    [Fact]
    public void FetchingAnUnpublishedRelease_Fails()
    {
        // The tag exists but its payload was never published; the operator must be told that, not
        // left with an empty directory that looks like a successful fetch.
        Git(_work, "tag", "-a", "v3.0.0", "-m", "ITAdmin 3.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");

        var consumer = Path.Combine(_root, "consumer-missing");
        Directory.CreateDirectory(consumer);
        Git(consumer, "init", "--quiet");
        Git(consumer, "remote", "add", "origin", _bare);

        var (exitCode, _, _) = TryGit(
            consumer,
            "fetch", "--depth", "1", "--quiet", "origin",
            GitReleaseRefs.DistributionRef(ReleaseVersion.Parse("3.0.0")));

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void HostAgentComponent_IsDeliveredBesideThePayloadAndVerified()
    {
        var version = ReleaseVersion.Parse("1.0.0");
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");
        var sourceCommit = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();

        PublishDistribution(version, sourceCommit, payloadFileCount: 3, includeHostAgent: true);
        var acquired = FetchDistribution(version);

        // Beside app/, never inside it: app/ becomes the IIS physicalPath, and privileged binaries
        // under the web root would defeat the boundary they exist to enforce.
        Assert.True(Directory.Exists(Path.Combine(acquired, DeploymentLayout.HostAgentDirectoryName)));
        Assert.False(Directory.Exists(
            Path.Combine(acquired, DeploymentLayout.PayloadDirectoryName, DeploymentLayout.HostAgentDirectoryName)));

        var result = ReleaseAcquisition.Verify(acquired, version, sourceCommit);

        Assert.True(result.IsAcceptable, string.Join("; ", result.Problems));
        Assert.NotNull(result.Manifest!.HostAgentComponent);
        Assert.Equal(2, result.Manifest.HostAgentComponent!.Integrity.FileCount);
    }

    [Fact]
    public void TamperedHostAgentComponent_IsRefused()
    {
        // A privileged LocalSystem service is a strange place to relax an integrity check.
        var version = ReleaseVersion.Parse("1.0.0");
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");
        var sourceCommit = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();

        PublishDistribution(version, sourceCommit, payloadFileCount: 3, includeHostAgent: true);
        var acquired = FetchDistribution(version);

        File.AppendAllText(
            Path.Combine(acquired, DeploymentLayout.HostAgentDirectoryName, "ITAdmin.HostAgent.dll"),
            "tampered");

        var result = ReleaseAcquisition.Verify(acquired, version, sourceCommit);

        Assert.False(result.IsAcceptable);
        Assert.Contains(result.Problems, problem => problem.Contains("altered:", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingHostAgentDirectory_WhenDeclared_IsRefused()
    {
        var version = ReleaseVersion.Parse("1.0.0");
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");
        var sourceCommit = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();

        PublishDistribution(version, sourceCommit, payloadFileCount: 3, includeHostAgent: true);
        var acquired = FetchDistribution(version);

        Directory.Delete(Path.Combine(acquired, DeploymentLayout.HostAgentDirectoryName), recursive: true);

        var result = ReleaseAcquisition.Verify(acquired, version, sourceCommit);

        Assert.False(result.IsAcceptable);
        Assert.Contains(result.Problems, problem => problem.Contains("hostagent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReleaseWithoutAHostAgentComponent_IsRefused()
    {
        var version = ReleaseVersion.Parse("1.0.0");
        Git(_work, "tag", "-a", "v1.0.0", "-m", "ITAdmin 1.0.0");
        Git(_work, "push", "--quiet", "origin", "--tags");
        var sourceCommit = Git(_work, "rev-parse", "refs/tags/v1.0.0^{commit}").Trim();

        PublishDistribution(version, sourceCommit, payloadFileCount: 3);
        var acquired = FetchDistribution(version);
        var manifestPath = Path.Combine(acquired, ReleaseManifest.FileName);
        var manifest = ReleaseManifest.FromJson(File.ReadAllText(manifestPath))!;
        var components = manifest.Components
            .Where(entry => entry.Key != DeploymentLayout.HostAgentDirectoryName)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        File.WriteAllText(manifestPath, (manifest with { Components = components }).ToJson());

        var result = ReleaseAcquisition.Verify(acquired, version, sourceCommit);

        Assert.False(result.IsAcceptable);
        Assert.Contains(result.Problems, problem => problem.Contains("hostagent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingReleaseDirectory_IsReportedRatherThanThrowing()
    {
        var result = ReleaseAcquisition.Verify(
            Path.Combine(_root, "does-not-exist"),
            ReleaseVersion.Parse("1.0.0"),
            new string('a', 40));

        Assert.False(result.IsAcceptable);
        Assert.Single(result.Problems);
    }

    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a release tree, commits it as an orphan, and pushes it to the distribution ref -
    /// the same sequence the publish pipeline performs.
    /// </summary>
    private void PublishDistribution(
        ReleaseVersion version,
        string sourceCommit,
        int payloadFileCount,
        string? manifestVersionOverride = null,
        bool includeHostAgent = false)
    {
        var publish = Path.Combine(_root, "publish-" + version + "-" + Guid.NewGuid().ToString("N")[..6]);
        var payload = Path.Combine(publish, "payload");
        Directory.CreateDirectory(Path.Combine(payload, "wwwroot"));

        // The packer requires a payload that could actually serve.
        File.WriteAllText(Path.Combine(payload, "ITAdmin.Api.dll"), "fake assembly " + version);
        File.WriteAllText(Path.Combine(payload, "web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(payload, "wwwroot", "index.html"), "<html></html>");
        for (var index = 3; index < payloadFileCount; index++)
        {
            File.WriteAllText(Path.Combine(payload, $"extra-{index}.txt"), $"content {index}");
        }

        var hostAgentPublish = Path.Combine(publish, "hostagent-publish");
        Directory.CreateDirectory(hostAgentPublish);
        File.WriteAllText(Path.Combine(hostAgentPublish, "ITAdmin.HostAgent.dll"), "fake agent " + version);
        File.WriteAllText(Path.Combine(hostAgentPublish, "ITAdmin.HostAgent.runtimeconfig.json"), "{}");
        var deploymentTooling = Path.Combine(publish, "deployment-tooling");
        Directory.CreateDirectory(deploymentTooling);
        File.WriteAllText(Path.Combine(deploymentTooling, "Install-ITAdmin.ps1"), "# fake installer");
        var updateCoordinator = Path.Combine(publish, "update-coordinator");
        Directory.CreateDirectory(updateCoordinator);
        File.WriteAllText(Path.Combine(updateCoordinator, "ITAdmin.UpdateCoordinator.exe"), "fake coordinator");

        var tree = Path.Combine(publish, "tree");
        var manifest = ReleasePacker.StageReleaseTree(
            new ReleasePacker.PackRequest(
                PublishDirectory: payload,
                OutputDirectory: tree,
                Version: version,
                SourceCommit: sourceCommit,
                BuildTimestampUtc: DateTimeOffset.UnixEpoch,
                LatestMigration: "20240101000000_Initial",
                MigrationCount: 1,
                HostAgentPublishDirectory: hostAgentPublish,
                DeploymentToolingDirectory: deploymentTooling,
                UpdateCoordinatorPublishDirectory: updateCoordinator),
            tree);

        Assert.Equal(version.ToString(), manifest.Source.Version);

        if (manifestVersionOverride is not null)
        {
            var manifestPath = Path.Combine(tree, ReleaseManifest.FileName);
            File.WriteAllText(
                manifestPath,
                File.ReadAllText(manifestPath).Replace(
                    $"\"version\": \"{version}\"",
                    $"\"version\": \"{manifestVersionOverride}\"",
                    StringComparison.Ordinal));
        }

        var distRepo = Path.Combine(publish, "dist-repo");
        Directory.CreateDirectory(distRepo);
        Git(distRepo, "init", "--quiet");
        Git(distRepo, "config", "user.name", "ITAdmin Release");
        Git(distRepo, "config", "user.email", "release@itadmin.invalid");
        CopyDirectory(tree, distRepo);
        Git(distRepo, "add", "--all");
        Git(distRepo, "commit", "--quiet", "-m", $"ITAdmin {version} Windows payload");
        Git(distRepo, "push", "--quiet", _bare, $"HEAD:{GitReleaseRefs.DistributionRef(version)}");
    }

    private string FetchDistribution(ReleaseVersion version)
    {
        var consumer = Path.Combine(_root, "fetch-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(consumer);
        Git(consumer, "init", "--quiet");
        Git(consumer, "remote", "add", "origin", _bare);
        Git(consumer, "fetch", "--depth", "1", "--quiet", "origin", GitReleaseRefs.DistributionRef(version));
        Git(consumer, "checkout", "--quiet", "FETCH_HEAD");
        return consumer;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
        }
    }

    private static string Git(string workingDirectory, params string[] arguments)
    {
        var (exitCode, standardOutput, standardError) = TryGit(workingDirectory, arguments);
        Assert.True(
            exitCode == 0,
            $"git {string.Join(' ', arguments)} exited {exitCode}: {standardError}");
        return standardOutput;
    }

    private static (int ExitCode, string StandardOutput, string StandardError) TryGit(
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Keep the fixture independent of whatever the developer's global Git config says.
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["HOME"] = workingDirectory;

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
        process.WaitForExit();

        return (process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }
}
