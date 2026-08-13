using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

public sealed class ReleaseIntegrityTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "itadmin-integrity-" + Guid.NewGuid().ToString("N"));

    public ReleaseIntegrityTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    [Fact]
    public void Create_HashesEveryFileWithForwardSlashPaths()
    {
        WriteFile("app/ITAdmin.Api.dll", "binary");
        WriteFile("app/wwwroot/index.html", "<html></html>");

        var integrity = ReleaseIntegrity.Create(root);

        Assert.Equal(2, integrity.FileCount);
        Assert.Equal(ReleaseIntegrity.Sha256, integrity.Algorithm);
        Assert.Contains("app/ITAdmin.Api.dll", integrity.Files.Keys);
        Assert.Contains("app/wwwroot/index.html", integrity.Files.Keys);
        Assert.All(integrity.Files.Values, digest => Assert.True(ReleaseIntegrity.IsSha256Hex(digest)));
        Assert.All(integrity.Files.Keys, path => Assert.DoesNotContain('\\', path));
        Assert.True(integrity.Validate().Count == 0);
    }

    [Fact]
    public void Verify_UnmodifiedPayload_IsValid()
    {
        WriteFile("app/one.txt", "one");
        WriteFile("app/two.txt", "two");

        var result = ReleaseIntegrity.Create(root).Verify(root);

        Assert.True(result.IsValid);
        Assert.Empty(result.Describe());
    }

    [Fact]
    public void Verify_AlteredFile_IsDetected()
    {
        WriteFile("app/one.txt", "original");
        var integrity = ReleaseIntegrity.Create(root);
        WriteFile("app/one.txt", "tampered");

        var result = integrity.Verify(root);

        Assert.False(result.IsValid);
        Assert.Equal(["app/one.txt"], result.Altered);
        Assert.Contains("altered: app/one.txt", result.Describe());
    }

    [Fact]
    public void Verify_MissingFile_IsDetected()
    {
        WriteFile("app/one.txt", "one");
        var path = WriteFile("app/two.txt", "two");
        var integrity = ReleaseIntegrity.Create(root);
        File.Delete(path);

        var result = integrity.Verify(root);

        Assert.False(result.IsValid);
        Assert.Equal(["app/two.txt"], result.Missing);
    }

    [Fact]
    public void Verify_UnexpectedExtraFile_IsDetected()
    {
        WriteFile("app/one.txt", "one");
        var integrity = ReleaseIntegrity.Create(root);
        WriteFile("app/smuggled.dll", "payload");

        var result = integrity.Verify(root);

        // A release directory must be an exact reproduction of the build output; an extra file
        // means something wrote into it after staging.
        Assert.False(result.IsValid);
        Assert.Equal(["app/smuggled.dll"], result.Unexpected);
    }

    [Fact]
    public void Verify_TruncatedFile_IsDetected()
    {
        WriteFile("app/one.txt", "the full contents of the file");
        var integrity = ReleaseIntegrity.Create(root);
        WriteFile("app/one.txt", "the full");

        Assert.False(integrity.Verify(root).IsValid);
    }

    [Theory]
    [InlineData("app/ITAdmin.Api.dll", true)]
    [InlineData("app/wwwroot/assets/index-abc.js", true)]
    [InlineData("release.manifest.json", true)]
    [InlineData("app\\ITAdmin.Api.dll", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("C:/Windows/System32/evil.dll", false)]
    [InlineData("../../Windows/System32/evil.dll", false)]
    [InlineData("app/../../escape.txt", false)]
    [InlineData("app//double.txt", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsNormalisedRelativePath_RejectsEscapingOrPlatformSpecificPaths(string? path, bool expected) =>
        Assert.Equal(expected, ReleaseIntegrity.IsNormalisedRelativePath(path));

    [Fact]
    public void Validate_PathThatCouldEscapeThePayloadRoot_IsRejected()
    {
        var integrity = new ReleaseIntegrity
        {
            FileCount = 1,
            Files = new Dictionary<string, string> { ["../../Windows/System32/evil.dll"] = new string('a', 64) },
        };

        Assert.Contains(
            integrity.Validate(),
            error => error.Contains("normalised relative payload path", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("ZZZZ", false)]
    public void IsSha256Hex_RejectsNonDigests(string? digest, bool expected) =>
        Assert.Equal(expected, ReleaseIntegrity.IsSha256Hex(digest));

    [Fact]
    public void IsSha256Hex_AcceptsLowercaseHexDigest() =>
        Assert.True(ReleaseIntegrity.IsSha256Hex(new string('a', 64)));

    [Fact]
    public void ComputeFileDigest_MatchesKnownSha256()
    {
        // SHA-256 of "abc" — pins the algorithm so a future change cannot silently swap it.
        var path = WriteFile("abc.txt", "abc");

        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            ReleaseIntegrity.ComputeFileDigest(path));
    }
}
