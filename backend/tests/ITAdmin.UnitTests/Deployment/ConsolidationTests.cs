using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The consolidation pass: prerequisite chunking, the first-hop SSH contract, durable operation
/// state, and installation readiness. These are the pieces that between them remove the last manual
/// file transfer from a normal installation, so each one is pinned directly.
/// </summary>
public sealed class ConsolidationTests
{
    // ==========================================================================================
    // Prerequisite chunking - how an oversized third-party installer reaches the server
    // ==========================================================================================

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "itadmin-chunk-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private static byte[] DeterministicBytes(int length)
    {
        var bytes = new byte[length];
        for (var index = 0; index < length; index++)
        {
            bytes[index] = (byte)((index * 31) % 251);
        }

        return bytes;
    }

    private static PrerequisitePayload PayloadFrom(
        PrerequisiteChunkingResult chunking,
        string componentPath = "prerequisites/example") =>
        new()
        {
            Name = "Example Prerequisite",
            Version = "1.2.3",
            FileName = chunking.FileName,
            ComponentPath = componentPath,
            Sha256 = chunking.Sha256,
            SizeBytes = chunking.SizeBytes,
            ChunkDigests = chunking.ChunkDigests,
            SourceUrl = "https://example.invalid/example.exe",
            // Upstream provenance is mandatory on a real payload; supplied here so these tests
            // exercise chunking rather than re-testing manifest validation.
            UpstreamHash = new string('e', 128),
            UpstreamHashAlgorithm = UpstreamHashAlgorithm.Sha512,
            UpstreamHashSource = "test fixture",
        };

    [Fact]
    public void Chunking_RoundTripsAFileLargerThanOneChunk()
    {
        using var temp = new TempDirectory();
        var source = temp.File("installer.exe");
        File.WriteAllBytes(source, DeterministicBytes(5000));

        var chunks = System.IO.Path.Combine(temp.Path, "chunks");
        var chunking = PrerequisiteChunking.Split(source, chunks, chunkBytes: 1024);

        Assert.Equal(5, chunking.ChunkDigests.Count);
        Assert.Equal(5000, chunking.SizeBytes);

        var destination = temp.File("reassembled.exe");
        var result = PrerequisiteChunking.Reassemble(PayloadFrom(chunking), chunks, destination);

        Assert.True(result.Succeeded, string.Join("; ", result.Problems));
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(destination));
    }

    [Fact]
    public void Chunking_ChunksStayWithinTheDeclaredBound()
    {
        // The whole point: no single object may approach a Git host's per-file limit.
        using var temp = new TempDirectory();
        var source = temp.File("installer.exe");
        File.WriteAllBytes(source, DeterministicBytes(5000));

        var chunks = System.IO.Path.Combine(temp.Path, "chunks");
        PrerequisiteChunking.Split(source, chunks, chunkBytes: 1024);

        foreach (var chunk in Directory.EnumerateFiles(chunks))
        {
            Assert.True(new FileInfo(chunk).Length <= 1024);
        }
    }

    [Fact]
    public void Chunking_DefaultChunkSizeIsWellUnderTheGitHostLimit()
    {
        // 32 MiB: below the ~50 MB warning threshold and far below the ~100 MB hard rejection.
        Assert.Equal(32 * 1024 * 1024, PrerequisiteChunking.DefaultChunkBytes);
        Assert.True(PrerequisiteChunking.DefaultChunkBytes < 50 * 1000 * 1000);
    }

    [Fact]
    public void Reassembly_VerifiesTheWholeFile_NotJustTheChunks()
    {
        // Individually valid chunks in the wrong order reconstruct into something nobody released.
        // Only the full-file digest catches that, which is why it is the gate that authorises
        // execution.
        using var temp = new TempDirectory();
        var source = temp.File("installer.exe");
        File.WriteAllBytes(source, DeterministicBytes(3000));

        var chunks = System.IO.Path.Combine(temp.Path, "chunks");
        var chunking = PrerequisiteChunking.Split(source, chunks, chunkBytes: 1000);

        // Swap the digests of chunks 0 and 1: each chunk still hashes to a listed value, but the
        // reassembled file is a different file.
        var reordered = chunking.ChunkDigests.ToList();
        (reordered[0], reordered[1]) = (reordered[1], reordered[0]);

        var payload = PayloadFrom(chunking) with { ChunkDigests = reordered };
        var result = PrerequisiteChunking.Reassemble(payload, chunks, temp.File("out.exe"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Reassembly_TamperedChunk_IsRefused()
    {
        using var temp = new TempDirectory();
        var source = temp.File("installer.exe");
        File.WriteAllBytes(source, DeterministicBytes(3000));

        var chunks = System.IO.Path.Combine(temp.Path, "chunks");
        var chunking = PrerequisiteChunking.Split(source, chunks, chunkBytes: 1000);

        File.WriteAllBytes(
            System.IO.Path.Combine(chunks, PrerequisiteChunking.ChunkFileName(chunking.FileName, 1)),
            DeterministicBytes(1000).Select(b => (byte)(b ^ 0xFF)).ToArray());

        var result = PrerequisiteChunking.Reassemble(PayloadFrom(chunking), chunks, temp.File("out.exe"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Problems, problem => problem.Contains("chunk 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Reassembly_MissingChunk_IsRefused()
    {
        using var temp = new TempDirectory();
        var source = temp.File("installer.exe");
        File.WriteAllBytes(source, DeterministicBytes(3000));

        var chunks = System.IO.Path.Combine(temp.Path, "chunks");
        var chunking = PrerequisiteChunking.Split(source, chunks, chunkBytes: 1000);

        File.Delete(System.IO.Path.Combine(chunks, PrerequisiteChunking.ChunkFileName(chunking.FileName, 2)));

        var result = PrerequisiteChunking.Reassemble(PayloadFrom(chunking), chunks, temp.File("out.exe"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Problems, problem => problem.Contains("missing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Reassembly_MismatchedFullFileDigest_LeavesNoExecutableBehind()
    {
        // A partially written or wrong installer must never be left somewhere something might run it.
        using var temp = new TempDirectory();
        var source = temp.File("installer.exe");
        File.WriteAllBytes(source, DeterministicBytes(2000));

        var chunks = System.IO.Path.Combine(temp.Path, "chunks");
        var chunking = PrerequisiteChunking.Split(source, chunks, chunkBytes: 1000);

        var payload = PayloadFrom(chunking) with { Sha256 = new string('f', 64) };
        var destination = temp.File("out.exe");

        var result = PrerequisiteChunking.Reassemble(payload, chunks, destination);

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(destination));
        Assert.False(File.Exists(destination + ".reassembling"));
    }

    [Fact]
    public void Reassembly_SizeMismatch_IsRefused()
    {
        using var temp = new TempDirectory();
        var source = temp.File("installer.exe");
        File.WriteAllBytes(source, DeterministicBytes(2000));

        var chunks = System.IO.Path.Combine(temp.Path, "chunks");
        var chunking = PrerequisiteChunking.Split(source, chunks, chunkBytes: 1000);

        var result = PrerequisiteChunking.Reassemble(
            PayloadFrom(chunking) with { SizeBytes = 999 },
            chunks,
            temp.File("out.exe"));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("../escape.exe")]
    [InlineData("dir/file.exe")]
    [InlineData("dir\\file.exe")]
    [InlineData("C:evil.exe")]
    [InlineData("")]
    public void Chunking_RejectsUnsafeFileNames(string fileName) =>
        Assert.False(PrerequisiteChunking.IsSafeFileName(fileName));

    // ==========================================================================================
    // Repository-controlled prerequisite metadata
    // ==========================================================================================

    [Fact]
    public void PrerequisiteMetadata_NonHttpsSource_IsRejected()
    {
        // A plaintext download of an executable that will run as SYSTEM invites a downgrade to a
        // stale-but-correctly-hashed vulnerable build.
        var metadata = new PrerequisiteRequirementMetadata
        {
            ComponentName = "Example",
            Pinned = new PinnedPrerequisite
            {
                Version = "1.0.0",
                FileName = "example.exe",
                SourceUrl = "http://example.invalid/example.exe",
                HashAlgorithm = UpstreamHashAlgorithm.Sha512,
                ExpectedHash = new string('a', 128),
                HashSource = "vendor",
            },
        };

        Assert.Contains(metadata.Validate(), problem => problem.Contains("https", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PrerequisiteMetadata_MatchesTheHostingBundleRequirementContract() =>
        Assert.Equal(
            AspNetCoreHostingBundleRequirement.MajorVersion,
            PrerequisiteRequirementMetadata.FromJson(
                File.ReadAllText(Path.Combine(
                    RepositoryRootLocator.Find(),
                    "scripts", "install", "prerequisites", "hosting-bundle.requirement.json")))!.MajorVersion);

    // ==========================================================================================
    // First-hop SSH contract
    // ==========================================================================================

    [Theory]
    [InlineData("git@github.com:contoso/itadmin.git", "github.com")]
    [InlineData("ssh://git@github.com/contoso/itadmin.git", "github.com")]
    [InlineData("ssh://git@git.corp.example.com:2222/contoso/itadmin.git", "git.corp.example.com")]
    [InlineData("git@git.corp.example.com:group/sub/itadmin.git", "git.corp.example.com")]
    public void SshHost_IsExtractedFromEitherRemoteForm(string url, string expected)
    {
        Assert.True(RepositoryAccessContract.TryGetSshHost(url, out var host, out _));
        Assert.Equal(expected, host);
    }

    [Theory]
    [InlineData("https://github.com/contoso/itadmin.git")]
    [InlineData("")]
    [InlineData("not a url")]
    public void SshHost_NonSshRemote_IsRejected(string url) =>
        Assert.False(RepositoryAccessContract.TryGetSshHost(url, out _, out _));

    [Fact]
    public void SshConfigEntry_UsesAnITAdminAliasRatherThanCapturingTheRealHost()
    {
        // "Host github.com" would route EVERY GitHub SSH operation this administrator performs
        // through a read-only deploy key scoped to one repository - silently breaking their
        // unrelated work. The alias confines the key to ITAdmin's clone.
        var entry = RepositoryAccessContract.BuildSshConfigEntry(
            "github.com",
            @"C:\Users\admin\.ssh\itadmin_deploy");

        Assert.Contains("Host github-itadmin", entry, StringComparison.Ordinal);
        Assert.Contains("HostName github.com", entry, StringComparison.Ordinal);
        Assert.Contains(@"IdentityFile C:\Users\admin\.ssh\itadmin_deploy", entry, StringComparison.Ordinal);
        // Without IdentitiesOnly, OpenSSH still offers agent and default identities first.
        Assert.Contains("IdentitiesOnly yes", entry, StringComparison.Ordinal);

        // Crucially, the stanza must NOT declare the real host as the matched pattern.
        Assert.DoesNotContain("Host github.com", entry, StringComparison.Ordinal);
    }

    [Fact]
    public void SshAlias_ProducesTheDocumentedCloneUrl() =>
        Assert.Equal(
            "git@github-itadmin:contoso/itadmin.git",
            RepositoryAccessContract.BuildAliasCloneUrl("contoso", "itadmin"));

    [Fact]
    public void SshAlias_IsResolvedToTheRealHostForMachineConfiguration()
    {
        // The alias only resolves inside the profile holding the SSH config entry. Persisting it
        // would break the Host Agent the moment that profile is removed.
        Assert.Equal(
            "git@github.com:contoso/itadmin.git",
            RepositoryAccessContract.ResolveAliasToRealHost(
                "git@github-itadmin:contoso/itadmin.git",
                "github.com"));

        Assert.Equal(
            "ssh://git@github.com/contoso/itadmin.git",
            RepositoryAccessContract.ResolveAliasToRealHost(
                "ssh://git@github-itadmin/contoso/itadmin.git",
                "github.com"));
    }

    [Fact]
    public void SshAlias_ResolutionLeavesANonAliasRemoteAlone() =>
        Assert.Equal(
            "git@github.com:contoso/itadmin.git",
            RepositoryAccessContract.ResolveAliasToRealHost(
                "git@github.com:contoso/itadmin.git",
                "github.com"));

    [Fact]
    public void SshAlias_IsRecognisedAsAnSshHost()
    {
        Assert.True(RepositoryAccessContract.TryGetSshHost(
            "git@github-itadmin:contoso/itadmin.git",
            out var host,
            out _));

        Assert.Equal(RepositoryAccessContract.SshHostAlias, host);
    }

    [Fact]
    public void SshCommand_WithoutAKnownHostsFile_StillKeepsStrictChecking()
    {
        var command = RepositoryAccessContract.BuildSshCommand(@"C:\keys\deploy_key", knownHostsPath: null);

        Assert.Contains("StrictHostKeyChecking=yes", command, StringComparison.Ordinal);
        Assert.DoesNotContain("accept-new", command, StringComparison.Ordinal);
        Assert.DoesNotContain("UserKnownHostsFile", command, StringComparison.Ordinal);
    }

    [Fact]
    public void SshCommand_WithMachineKnownHosts_DoesNotDependOnAUserProfile()
    {
        // The Host Agent runs as LocalSystem and cannot read an administrator's profile - which may
        // also be deleted with the account.
        var command = RepositoryAccessContract.BuildSshCommand(
            @"C:\ProgramData\ITAdmin\keys\deploy_key",
            @"C:\ProgramData\ITAdmin\keys\known_hosts");

        Assert.Contains(@"UserKnownHostsFile=""C:\ProgramData\ITAdmin\keys\known_hosts""", command, StringComparison.Ordinal);
        Assert.Contains("GlobalKnownHostsFile=/dev/null", command, StringComparison.Ordinal);
        Assert.DoesNotContain("USERPROFILE", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\Users\", command, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("github.com ssh-ed25519 AAAAC3Nz", "github.com", true)]
    [InlineData("github.com,140.82.121.4 ssh-rsa AAAAB3Nz", "github.com", true)]
    [InlineData("[git.example.com]:2222 ssh-ed25519 AAAAC3Nz", "git.example.com", true)]
    [InlineData("|1|hashedbase64|hashedbase64 ssh-ed25519 AAAAC3Nz", "github.com", true)]
    [InlineData("gitlab.com ssh-ed25519 AAAAC3Nz", "github.com", false)]
    [InlineData("# only a comment", "github.com", false)]
    [InlineData("", "github.com", false)]
    public void KnownHosts_HostEntryDetection(string content, string host, bool expected) =>
        Assert.Equal(expected, RepositoryAccessContract.ContainsHostEntry(content, host));

    // ==========================================================================================
    // Durable deployment operation state
    // ==========================================================================================

    private static DeploymentOperation OperationAt(DeploymentOperationStage stage) =>
        DeploymentOperation.Start("op1", DeploymentOperationKind.Update, "2.1.0", DateTimeOffset.UnixEpoch)
            .Advance(stage, "in flight", DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData(DeploymentOperationStage.Resolving, InterruptedOperationDisposition.SafeToDiscard)]
    [InlineData(DeploymentOperationStage.Fetching, InterruptedOperationDisposition.SafeToDiscard)]
    [InlineData(DeploymentOperationStage.Verifying, InterruptedOperationDisposition.SafeToDiscard)]
    [InlineData(DeploymentOperationStage.Staging, InterruptedOperationDisposition.RetryFromStart)]
    [InlineData(DeploymentOperationStage.Migrating, InterruptedOperationDisposition.RequiresOperatorReview)]
    [InlineData(DeploymentOperationStage.Activating, InterruptedOperationDisposition.RequiresOperatorReview)]
    public void InterruptedOperation_IsClassifiedByHowFarItGot(
        DeploymentOperationStage stage,
        InterruptedOperationDisposition expected) =>
        // The distinction that matters is whether the database or the live site could have been
        // partially changed. Those need a human; an interrupted fetch is simply discardable.
        Assert.Equal(expected, OperationAt(stage).Classify());

    [Theory]
    [InlineData(DeploymentOperationStage.Completed)]
    [InlineData(DeploymentOperationStage.Failed)]
    public void TerminalOperation_IsNotInterrupted(DeploymentOperationStage stage)
    {
        var operation = OperationAt(stage);

        Assert.True(operation.IsTerminal);
        Assert.Equal(InterruptedOperationDisposition.Complete, operation.Classify());
    }

    [Fact]
    public void InstallationState_WithAnInFlightOperation_ReportsItAsInterrupted()
    {
        var state = InstallationState.Fresh(DateTimeOffset.UnixEpoch) with
        {
            CurrentOperation = OperationAt(DeploymentOperationStage.Migrating),
        };

        Assert.True(state.HasInterruptedOperation);
    }

    [Fact]
    public void InstallationState_OperationSurvivesSerialisation()
    {
        // The entire point: a service restart must be able to read this back.
        var state = InstallationState.Fresh(DateTimeOffset.UnixEpoch) with
        {
            CurrentOperation = OperationAt(DeploymentOperationStage.Staging),
        };

        var restored = InstallationState.FromJson(state.ToJson());

        Assert.NotNull(restored);
        Assert.True(restored!.HasInterruptedOperation);
        Assert.Equal(DeploymentOperationStage.Staging, restored.CurrentOperation!.Stage);
        Assert.Equal("2.1.0", restored.CurrentOperation.TargetVersion);
    }

    [Fact]
    public void DeploymentOperation_CarriesNoSecretsOrPaths()
    {
        var json = InstallationState.Fresh(DateTimeOffset.UnixEpoch) with
        {
            CurrentOperation = OperationAt(DeploymentOperationStage.Fetching),
        };

        foreach (var term in new[] { "password", "secret", "key", "deploy_key", "repositoryUrl", "git@" })
        {
            Assert.DoesNotContain(term, json.ToJson(), StringComparison.OrdinalIgnoreCase);
        }
    }

    // ==========================================================================================
    // Installation readiness - "installed" must mean "somebody can log in"
    // ==========================================================================================

    [Fact]
    public void Readiness_AllFourConditions_AreRequired()
    {
        var complete = new InstallationReadiness
        {
            ProcessHealthy = true,
            SetupCompleted = true,
            DirectoryUsable = true,
            AdministratorBootstrapped = true,
        };

        Assert.True(complete.IsComplete);
        Assert.Empty(complete.Describe());
    }

    [Fact]
    public void Readiness_ServingButNoDirectory_IsNotComplete()
    {
        // The exact case this exists for: a worker process answering HTTP 200 while nobody can
        // authenticate, because ITAdmin logs in through LDAP.
        var readiness = new InstallationReadiness { ProcessHealthy = true };

        Assert.False(readiness.IsComplete);
        Assert.Contains(readiness.Describe(), note => note.Contains("Primary Directory", StringComparison.Ordinal));
        Assert.Contains(readiness.Describe(), note => note.Contains("administrator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InstallationState_InstalledWithoutReadiness_IsNotUsable()
    {
        var state = InstallationState.Fresh(DateTimeOffset.UnixEpoch) with
        {
            Phase = InstallationPhase.Installed,
            ActiveVersion = "2.1.0",
        };

        Assert.False(state.IsUsable);
    }

    [Fact]
    public void InstallationState_InstalledWithFullReadiness_IsUsable()
    {
        var state = InstallationState.Fresh(DateTimeOffset.UnixEpoch) with
        {
            Phase = InstallationPhase.Installed,
            ActiveVersion = "2.1.0",
            Readiness = new InstallationReadiness
            {
                ProcessHealthy = true,
                SetupCompleted = true,
                DirectoryUsable = true,
                AdministratorBootstrapped = true,
            },
        };

        Assert.True(state.IsUsable);
    }

    [Fact]
    public void InstallationState_MigrationInFlight_IsNeverUsable()
    {
        var state = InstallationState.Fresh(DateTimeOffset.UnixEpoch) with
        {
            Phase = InstallationPhase.Installed,
            MigrationInFlight = true,
            Readiness = new InstallationReadiness
            {
                ProcessHealthy = true,
                SetupCompleted = true,
                DirectoryUsable = true,
                AdministratorBootstrapped = true,
            },
        };

        Assert.False(state.IsUsable);
    }

    // ==========================================================================================
    // Distribution ref diagnostics
    // ==========================================================================================

    [Fact]
    public void RefDiagnostics_DistinguishNamespaceAbsenceFromReleaseAbsence()
    {
        // The single most useful distinction when deploying against a new Git host for the first
        // time: "this host will not do custom namespaces" versus "nobody published this release".
        string[] noNamespace = [$"{new string('a', 40)}\trefs/tags/v1.0.0"];
        string[] withNamespace = [$"{new string('a', 40)}\trefs/itadmin/dist/1.0.0"];

        Assert.False(DistributionRefDiagnostics.AdvertisesDistributionNamespace(noNamespace));
        Assert.True(DistributionRefDiagnostics.AdvertisesDistributionNamespace(withNamespace));
    }

    [Fact]
    public void RefDiagnostics_DetectASpecificReleasesRef()
    {
        string[] lines = [$"{new string('a', 40)}\trefs/itadmin/dist/1.0.0"];

        Assert.True(DistributionRefDiagnostics.AdvertisesRelease(lines, ReleaseVersion.Parse("1.0.0")));
        Assert.False(DistributionRefDiagnostics.AdvertisesRelease(lines, ReleaseVersion.Parse("2.0.0")));
    }

    [Fact]
    public void RefDiagnostics_NamespaceFailure_NamesTheSinglePointOfChange()
    {
        // If a Git host rejects the namespace tomorrow, the message must lead straight to the one
        // constant that has to change - not to a rewrite of the deployment engine.
        var message = DistributionRefDiagnostics.Describe(
            DistributionRefDiagnostics.Fault.NamespaceNotAdvertised);

        Assert.Contains("git ls-remote", message, StringComparison.Ordinal);
        Assert.Contains("DistributionRefPrefix", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefDiagnostics_EveryFaultProducesAnActionableMessage()
    {
        var version = ReleaseVersion.Parse("2.1.0");

        foreach (var fault in Enum.GetValues<DistributionRefDiagnostics.Fault>())
        {
            var message = DistributionRefDiagnostics.Describe(fault, version);

            Assert.False(string.IsNullOrWhiteSpace(message));
            // Each message must say what to do, not merely restate the symptom.
            Assert.True(message.Length > 80, $"{fault} message is too terse to act on.");
        }
    }

    [Fact]
    public void RefDiagnostics_IncludeGitOutputWhenAvailable()
    {
        var message = DistributionRefDiagnostics.Describe(
            DistributionRefDiagnostics.Fault.PublishRejected,
            ReleaseVersion.Parse("2.1.0"),
            "remote: refusing to create ref");

        Assert.Contains("remote: refusing to create ref", message, StringComparison.Ordinal);
    }
}

/// <summary>Locates the repository root from the test assembly, for reading checked-in files.</summary>
internal static class RepositoryRootLocator
{
    public static string Find([System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(callerFilePath)!);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
