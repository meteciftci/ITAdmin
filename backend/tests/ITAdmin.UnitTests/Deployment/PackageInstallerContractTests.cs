using System.Runtime.CompilerServices;

namespace ITAdmin.UnitTests.Deployment;

public sealed class PackageInstallerContractTests
{
    private static string RepositoryRoot([CallerFilePath] string callerFilePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(callerFilePath)!);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. segments]));

    private static string SetupSource() => Read("scripts", "install", "Setup-ITAdmin.ps1");
    private static string UpdateConfigSource() => Read("scripts", "install", "Configure-ITAdminUpdates.ps1");
    private static string PublishWorkflowSource() => Read(".github", "workflows", "publish-release.yml");

    [Fact]
    public void ProductionSetup_IsLocalPackageDrivenAndDoesNotInvokeGitOrSsh()
    {
        var source = SetupSource();

        Assert.Contains("self-contained release package", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("release.manifest.json", source, StringComparison.Ordinal);
        Assert.DoesNotContain("git clone", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git fetch", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GIT_SSH_COMMAND", source, StringComparison.Ordinal);
        Assert.DoesNotContain("& git", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("& ssh", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionSetup_VerifiesReleaseMatchedInstallerBeforeExecution()
    {
        var source = SetupSource();

        Assert.Contains("deployment-tooling\\Install-ITAdmin.ps1", source, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", source, StringComparison.Ordinal);
        Assert.Contains("SHA256", source, StringComparison.Ordinal);
        Assert.Contains("ExpectedVersion", source, StringComparison.Ordinal);
        Assert.Contains("ExpectedSourceCommit", source, StringComparison.Ordinal);
        Assert.Contains("failed SHA-256 verification and will not be executed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSetup_CreatesIisVirtualAccountBeforeFullInstall()
    {
        var source = SetupSource();
        var installStep = source.LastIndexOf("Installing the packaged release", StringComparison.Ordinal);
        var initializePool = source.LastIndexOf("Initialize-AppPoolIdentity", installStep, StringComparison.Ordinal);

        Assert.True(installStep > 0);
        Assert.True(initializePool > 0 && initializePool < installStep);
        Assert.Contains("PrerequisitesOnly", source, StringComparison.Ordinal);
        Assert.Contains("Application pool virtual account is ready for release ACLs", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSetup_LeavesRepositoryUpdatesDisabledOnFreshInstall()
    {
        var source = SetupSource();

        Assert.Contains("$updatesEnabled = $false", source, StringComparison.Ordinal);
        Assert.Contains("channel = $channel", source, StringComparison.Ordinal);
        Assert.Contains("updatesEnabled = $updatesEnabled", source, StringComparison.Ordinal);
        Assert.Contains("Preserved existing Host Agent update settings", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateConfiguration_RequiresExplicitVerifiedSshTrust()
    {
        var source = UpdateConfigSource();

        Assert.Contains("ssh-keygen -F", source, StringComparison.Ordinal);
        Assert.Contains("GIT_SSH_VARIANT", source, StringComparison.Ordinal);
        Assert.Contains("StrictHostKeyChecking=yes", source, StringComparison.Ordinal);
        Assert.Contains("IdentitiesOnly=yes", source, StringComparison.Ordinal);
        Assert.Contains("read-only Deploy Key", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("& ssh-keyscan", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StrictHostKeyChecking=no", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StrictHostKeyChecking=accept-new", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseWorkflow_PublishesOperatorZipChecksumAndGitHubRelease()
    {
        var source = PublishWorkflowSource();

        Assert.Contains("ITAdmin-$version-windows.zip", source, StringComparison.Ordinal);
        Assert.Contains("Compress-Archive", source, StringComparison.Ordinal);
        Assert.Contains(".sha256", source, StringComparison.Ordinal);
        Assert.Contains("Setup-ITAdmin.ps1", source, StringComparison.Ordinal);
        Assert.Contains("Configure-ITAdminUpdates.ps1", source, StringComparison.Ordinal);
        Assert.Contains("gh release create", source, StringComparison.Ordinal);
        Assert.Contains("gh release upload", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseWorkflow_RetainsGitDistributionOnlyForOptionalUpdates()
    {
        var source = PublishWorkflowSource();

        Assert.Contains("refs/itadmin/dist/", source, StringComparison.Ordinal);
        Assert.Contains("optional in-app updates", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first install", source, StringComparison.OrdinalIgnoreCase);
    }
}
