using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

public sealed class DeploymentLayoutTests
{
    private static readonly DeploymentLayout Layout = DeploymentLayout.Default();

    [Fact]
    public void Default_SplitsImmutableReleasesFromMutableMachineState()
    {
        Assert.StartsWith(@"C:\Program Files\ITAdmin", Layout.ReleasesRoot, StringComparison.Ordinal);

        foreach (var mutablePath in Layout.PreservedAcrossReleases())
        {
            Assert.StartsWith(@"C:\ProgramData\ITAdmin", mutablePath, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MachineStateNeverLivesInsideAReleaseDirectory()
    {
        // The core invariant that makes a release replaceable: replacing a release cannot delete
        // configuration, secrets, the DataProtection key ring, or installation state.
        var releasePayload = Layout.ReleasePayloadDirectory(ReleaseVersion.Parse("2.0.0"));

        foreach (var preserved in Layout.PreservedAcrossReleases())
        {
            Assert.False(
                preserved.StartsWith(Layout.ProgramFilesRoot, StringComparison.OrdinalIgnoreCase),
                $"{preserved} must not live under the installer-owned release root.");
            Assert.False(preserved.StartsWith(releasePayload, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ReleaseDirectory_IsVersionScoped()
    {
        var first = Layout.ReleaseDirectory(ReleaseVersion.Parse("2.0.0"));
        var second = Layout.ReleaseDirectory(ReleaseVersion.Parse("2.1.0"));

        Assert.EndsWith(@"\releases\2.0.0", first, StringComparison.Ordinal);
        Assert.EndsWith(@"\releases\2.1.0", second, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ManifestSitsOutsideTheWebServedPayload()
    {
        var version = ReleaseVersion.Parse("2.0.0");

        // IIS is pointed at the payload directory, so the manifest beside it is never web-reachable.
        Assert.Equal(
            Layout.ReleaseDirectory(version) + @"\app",
            Layout.ReleasePayloadDirectory(version));
        Assert.Equal(
            Layout.ReleaseDirectory(version) + @"\release.manifest.json",
            Layout.ReleaseManifestPath(version));
    }

    [Fact]
    public void RequiredDirectories_CoverBothRootsAndAreAbsolute()
    {
        var directories = Layout.RequiredDirectories();

        Assert.Contains(Layout.ReleasesRoot, directories);
        Assert.Contains(Layout.ConfigRoot, directories);
        Assert.Contains(Layout.SecretsRoot, directories);
        Assert.Contains(Layout.StateRoot, directories);
        Assert.Contains(Layout.DataProtectionKeysRoot, directories);
        Assert.All(directories, path => Assert.Matches(@"^[A-Za-z]:\\", path));
    }

    [Theory]
    [InlineData(@"C:\Program Files\ITAdmin\releases\2.0.0", true)]
    [InlineData(@"C:\Program Files\ITAdmin\releases\2.0.0\app", true)]
    [InlineData(@"C:\Program Files\ITAdmin\releases", false)]
    [InlineData(@"C:\Program Files\ITAdmin", false)]
    [InlineData(@"C:\ProgramData\ITAdmin\secrets", false)]
    [InlineData(@"C:\Windows\System32", false)]
    [InlineData(@"C:\", false)]
    [InlineData("", false)]
    public void IsWithinReleasesRoot_GuardsDestructiveCleanup(string path, bool expected) =>
        // Release removal is a recursive delete; this is the guard that stops a bad version string
        // from turning cleanup into deletion of an unrelated tree.
        Assert.Equal(expected, Layout.IsWithinReleasesRoot(path));

    [Fact]
    public void IsWithinReleasesRoot_DoesNotMatchASiblingDirectoryWithASharedPrefix() =>
        Assert.False(Layout.IsWithinReleasesRoot(@"C:\Program Files\ITAdmin\releases-backup\2.0.0"));

    [Fact]
    public void Layout_IsRelocatableForNonDefaultInstallRoots()
    {
        var custom = new DeploymentLayout(@"D:\Apps\ITAdmin", @"E:\State\ITAdmin");

        Assert.Equal(@"D:\Apps\ITAdmin\releases", custom.ReleasesRoot);
        Assert.Equal(@"E:\State\ITAdmin\secrets", custom.SecretsRoot);
        Assert.True(custom.IsWithinReleasesRoot(@"D:\Apps\ITAdmin\releases\2.0.0"));
        Assert.False(custom.IsWithinReleasesRoot(@"C:\Program Files\ITAdmin\releases\2.0.0"));
    }

    [Fact]
    public void Layout_ProducesWindowsPathsRegardlessOfBuildHostOs() =>
        // The build runs on macOS in this repo; layout strings must still be Windows paths.
        Assert.All(Layout.RequiredDirectories(), path => Assert.DoesNotContain('/', path));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsEmptyRoots(string value)
    {
        Assert.Throws<ArgumentException>(() => new DeploymentLayout(value, @"C:\ProgramData\ITAdmin"));
        Assert.Throws<ArgumentException>(() => new DeploymentLayout(@"C:\Program Files\ITAdmin", value));
    }
}
