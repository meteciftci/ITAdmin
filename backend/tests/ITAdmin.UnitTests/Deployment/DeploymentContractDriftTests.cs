using System.Runtime.CompilerServices;
using ITAdmin.Deployment;
using ITAdmin.HostAgent;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The deployment contract is produced by C# on the build machine and consumed by PowerShell on the
/// target. Nothing in the compiler connects the two, so these tests pin the shared field names, the
/// shared rules, and the environment-neutrality guarantees that both sides depend on.
/// </summary>
public sealed class DeploymentContractDriftTests
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

    private static string ReadScript(params string[] relativeSegments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. relativeSegments]));

    private static string InstallerSource() => ReadScript("scripts", "install", "Install-ITAdmin.ps1");

    private static string BootstrapSource() => ReadScript("scripts", "install", "Bootstrap-ITAdmin.ps1");

    private static string ReadinessScriptSource() => ReadScript("scripts", "dev", "Test-ItAdminAdReadiness.ps1");

    private static string BuildScriptSource() => ReadScript("scripts", "release", "build-release.zsh");

    private static string PublishScriptSource() => ReadScript("scripts", "release", "publish-release.zsh");

    private static string PublishWorkflowSource() => ReadScript(".github", "workflows", "publish-release.yml");

    // ------------------------------------------------------------------------------------------
    // Release manifest and installation state
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("product")]
    [InlineData("version")]
    [InlineData("sourceCommit")]
    [InlineData("migrations")]
    [InlineData("integrity")]
    public void Installer_ReadsTheManifestFieldsTheBuildEmits(string field) =>
        Assert.Contains(field, InstallerSource(), StringComparison.Ordinal);

    [Theory]
    [InlineData("phase")]
    [InlineData("activeVersion")]
    [InlineData("stagedVersion")]
    [InlineData("previousVersion")]
    [InlineData("lastMigrationApplied")]
    [InlineData("migrationInFlight")]
    [InlineData("lastError")]
    public void Installer_WritesTheInstallationStateFieldsTheContractDefines(string field) =>
        Assert.Contains(field, InstallerSource(), StringComparison.Ordinal);

    [Fact]
    public void Installer_UsesEveryLifecyclePhaseNameFromTheContract()
    {
        var source = InstallerSource();

        foreach (var phase in Enum.GetNames<InstallationPhase>())
        {
            Assert.Contains($"\"{phase}\"", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Installer_UsesTheSameFileNamesAsTheContract()
    {
        var source = InstallerSource();

        Assert.Contains(ReleaseManifest.FileName, source, StringComparison.Ordinal);
        Assert.Contains(InstallationState.FileName, source, StringComparison.Ordinal);
        Assert.Contains(EnvironmentConfig.FileName, source, StringComparison.Ordinal);
        Assert.Contains(DeploymentLayout.PayloadDirectoryName, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_WritesTheCurrentEnvironmentConfigSchemaVersion() =>
        Assert.Contains(
            $"schemaVersion   = {EnvironmentConfig.CurrentSchemaVersion}",
            InstallerSource(),
            StringComparison.Ordinal);

    // ------------------------------------------------------------------------------------------
    // Repository-driven bootstrap
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Bootstrap_UsesTheDistributionAndTagRefNamespacesFromTheContract()
    {
        var source = BootstrapSource();

        Assert.Contains(GitReleaseRefs.DistributionRefPrefix, source, StringComparison.Ordinal);
        Assert.Contains(GitReleaseRefs.TagRefPrefix, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_TreatsMainAsTransportOnly()
    {
        var source = BootstrapSource();

        // The application payload comes from a distribution ref pinned to an annotated tag. If the
        // bootstrap ever fetched application content from a branch, the release identity guarantee
        // would be silently gone.
        Assert.Contains("bootstrap TRANSPORT ONLY", source, StringComparison.Ordinal);
        Assert.DoesNotContain("origin/main", source, StringComparison.Ordinal);
        Assert.DoesNotContain("refs/heads/main", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_MirrorsTheAnnotatedStableTagRules()
    {
        var source = BootstrapSource();

        Assert.Contains("^{}", source, StringComparison.Ordinal);
        Assert.Contains("lightweight tag", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pre-release", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stable channel", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bootstrap_FetchesShallowlyRatherThanCloningHistory()
    {
        var source = BootstrapSource();

        Assert.Contains("--depth", source, StringComparison.Ordinal);

        // The operator's own one-time `git clone` is documented in the help text; what must never
        // appear is the bootstrap itself cloning, which would drag full history onto the server.
        Assert.DoesNotContain("Invoke-Git -Arguments @(\"clone\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("& git clone", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_IntroducesNoTokenBasedRepositoryAuthentication()
    {
        // Repository access stays a read-only SSH deploy key. A PAT, a gh login, or an API token
        // would be a long-lived credential on a customer server with a much wider blast radius.
        var source = BootstrapSource();

        foreach (var forbidden in new[] { "gh auth", "GITHUB_TOKEN", "personal access token", "ghp_", "x-access-token" })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Bootstrap_DoesNotWeakenSshHostKeyVerification()
    {
        var source = BootstrapSource();

        Assert.Contains("StrictHostKeyChecking=yes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StrictHostKeyChecking=no", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UserKnownHostsFile=/dev/null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_KeepsTheDeployKeyOutOfWebReachablePlaces()
    {
        var source = BootstrapSource();

        Assert.Contains("icacls", source, StringComparison.Ordinal);
        Assert.Contains("SYSTEM:F", source, StringComparison.Ordinal);
        // The key never lands under a release directory, where IIS would serve it.
        Assert.DoesNotContain("releases\\", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bootstrap_WritesTheHostAgentSettingsContract()
    {
        var source = BootstrapSource();

        Assert.Contains(HostAgentSettings.FileName, source, StringComparison.Ordinal);
        foreach (var field in new[]
                 {
                     "repositoryUrl", "channel", "deployKeyDirectory", "appPoolName", "updatesEnabled",
                 })
        {
            Assert.Contains(field, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Bootstrap_HandsOffToTheInstallerWithAVerifiedReleaseIdentity()
    {
        var source = BootstrapSource();

        foreach (var argument in new[] { "-ReleaseDirectory", "-ExpectedVersion", "-ExpectedSourceCommit" })
        {
            Assert.Contains(argument, source, StringComparison.Ordinal);
            Assert.Contains(argument.TrimStart('-'), InstallerSource(), StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Installer behaviour
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Installer_VerifiesPayloadIdentityAgainstTheReleaseTag()
    {
        var source = InstallerSource();

        Assert.Contains("Test-CommitsMatch", source, StringComparison.Ordinal);
        Assert.Contains("does not carry the release it claims to", source, StringComparison.Ordinal);
        // Source identity and distribution identity are cross-checked against each other too.
        Assert.Contains("does not match source release", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_InvokesTheApplicationOwnedMigrationRunner() =>
        // No EF CLI and no psql on the target: the release migrates itself.
        Assert.Contains("--migrate", InstallerSource(), StringComparison.Ordinal);

    [Fact]
    public void Installer_InvokesTheApplicationOwnedDirectoryBootstrap()
    {
        // Role seeding and administrator creation stay in the application's own setup service; a
        // PowerShell reimplementation would be a second, unverified definition of an administrator.
        var source = InstallerSource();

        Assert.Contains(DirectoryBootstrapRunner_BootstrapArgument, source, StringComparison.Ordinal);
        Assert.DoesNotContain("PortalUserRole", source, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO", source, StringComparison.OrdinalIgnoreCase);
    }

    private const string DirectoryBootstrapRunner_BootstrapArgument = "--bootstrap-directory";

    [Fact]
    public void Installer_DoesNotDependOnTargetSideBuildOrDatabaseToolchain()
    {
        var source = InstallerSource() + BootstrapSource();

        foreach (var tool in new[] { "psql.exe", "dotnet ef", "npm ", "dotnet publish", "dotnet build" })
        {
            Assert.DoesNotContain(tool, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Installer_CanonicalizesRelativePathsBeforeAnythingElse()
    {
        // A relative -ArtifactPath used to be resolved after the working directory had already
        // moved, producing "file not found" for a file the operator was standing in.
        var source = InstallerSource();

        Assert.Contains("Resolve-CallerPath", source, StringComparison.Ordinal);
        Assert.Contains("GetFullPath", source, StringComparison.Ordinal);

        var paramBlockEnd = source.IndexOf("\n)", source.IndexOf("\nparam(", StringComparison.Ordinal), StringComparison.Ordinal);
        var firstUse = source.IndexOf("$ArtifactPath = Resolve-CallerPath", StringComparison.Ordinal);
        Assert.True(firstUse > paramBlockEnd, "Paths must be canonicalized immediately after the param block.");
    }

    [Fact]
    public void Installer_InitialHostingIsHttpOnly()
    {
        var source = InstallerSource();

        // No certificate discovery, no certificate store access, and no FQDN requirement anywhere
        // in the initial-install path.
        Assert.DoesNotContain("Cert:\\LocalMachine", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-CertificateCandidates", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolve-CertificateThumbprint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("New-WebBinding -Name $Config.iis.siteName -Protocol \"https\"", source, StringComparison.Ordinal);

        Assert.Contains("httpPort", source, StringComparison.Ordinal);
        Assert.Contains("configured later from ITAdmin Settings", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_NeverRemovesTheHttpBinding()
    {
        // Removing HTTP before a working HTTPS binding exists is how an administrator locks
        // themselves out of a server.
        Assert.DoesNotContain("Remove-WebBinding", InstallerSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_EstablishesTheDirectoryBeforeReportingSuccess()
    {
        var source = InstallerSource();

        var bootstrapCall = source.IndexOf("Invoke-DirectoryBootstrap -PayloadRoot", StringComparison.Ordinal);
        // LastIndexOf: the function is declared earlier in the file than it is called.
        var summaryCall = source.LastIndexOf("Write-InstallationSummary -Config", StringComparison.Ordinal);

        Assert.True(bootstrapCall > 0, "The installer must bootstrap the directory.");
        Assert.True(summaryCall > bootstrapCall, "Success must not be reported before the directory exists.");
    }

    [Fact]
    public void Installer_NeverAsksForTheAdministratorsOwnPassword()
    {
        // Only the bind credential is needed. Asking a person for their AD password during an
        // installation is both unnecessary and a habit worth not teaching.
        var source = InstallerSource();

        Assert.DoesNotContain("AdministratorPassword", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InitialAdministratorPassword", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installer_GeneratesInternalSecretsRatherThanPromptingForThem()
    {
        var source = InstallerSource();

        Assert.Contains("New-RandomSecret", source, StringComparison.Ordinal);
        Assert.Contains("RandomNumberGenerator", source, StringComparison.Ordinal);
        Assert.Contains("setupKeyHash", source, StringComparison.Ordinal);

        // The JWT key and the setup key must never be prompted for.
        Assert.DoesNotContain("Read-Host \"JWT", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Read-Host \"Setup key", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installer_SummaryPrintsSecretLocationsNotSecretValues()
    {
        var source = InstallerSource();
        var summaryStart = source.IndexOf("function Write-InstallationSummary", StringComparison.Ordinal);
        Assert.True(summaryStart > 0);
        var summary = source[summaryStart..];

        Assert.Contains("Windows DPAPI (LocalMachine)", summary, StringComparison.Ordinal);
        Assert.Contains("SecretsRoot", summary, StringComparison.Ordinal);

        foreach (var forbidden in new[] { "$jwtKey", "$setupKey", "$Script:SetupKey", "$ConnectionString", "$plain" })
        {
            Assert.DoesNotContain(forbidden, summary, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Installer_KeepsSecretsOutOfTheEnvironmentConfigAndState()
    {
        var source = InstallerSource();

        // Secrets are DPAPI-protected under ProgramData; they must never be written into the
        // environment config JSON, installation state, or (plaintext) app pool environment.
        Assert.Contains("runtime.secrets.dpapi", source, StringComparison.Ordinal);
        Assert.Contains("DataProtectionScope]::LocalMachine", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$State.connectionString", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$Config.database.password", source, StringComparison.Ordinal);

        // Non-secret app pool variables remain; the connection string / JWT key must not be set there.
        Assert.Contains("ITADMIN_Secrets__Root", source, StringComparison.Ordinal);
        Assert.Contains("ITADMIN_ConnectionStrings__DefaultConnection", source, StringComparison.Ordinal);
        Assert.Contains("ITADMIN_Jwt__Key", source, StringComparison.Ordinal);
        Assert.Contains("Remove-AppPoolEnvironmentVariable", source, StringComparison.Ordinal);
        Assert.Contains("Save-MachineSecrets", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_PassesDirectoryCredentialsThroughAFileNotACommandLine()
    {
        // A process command line is readable by every user on the machine.
        var source = InstallerSource();

        Assert.Contains("--input $inputPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("--bind-password", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--setup-key", source, StringComparison.OrdinalIgnoreCase);

        // ...and the file is removed whatever happens.
        Assert.Contains("Remove-Item -LiteralPath $inputPath -Force", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ReadsSecretsWithoutEcho()
    {
        var source = InstallerSource();

        Assert.Contains("Read-Host -AsSecureString", source, StringComparison.Ordinal);
        Assert.Contains("[SecureString]$DirectoryBindPassword", source, StringComparison.Ordinal);
        Assert.Contains("[SecureString]$DatabasePassword", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_RequiredIisFeatures_MatchDeploymentContract()
    {
        var source = InstallerSource();

        foreach (var feature in IisPrerequisiteFeatures.RequiredNames)
        {
            Assert.Contains($"\"{feature}\"", source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("IncludeAllSubFeature:$true", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IncludeAllSubFeature -", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never IncludeAllSubFeature", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_HostingBundleDetection_DoesNotRequireAncmFileMajorToMatchTfm()
    {
        var source = InstallerSource();

        // Shared framework tracks TFM major; ANCM file version is diagnostic only.
        Assert.Contains("ANCM file/product version", source, StringComparison.Ordinal);
        Assert.Contains("is NOT the AspNetCore.App TFM major", source, StringComparison.Ordinal);
        Assert.Contains("SharedFrameworkOk", source, StringComparison.Ordinal);
        Assert.DoesNotContain("need major {2}", source, StringComparison.Ordinal);
        Assert.Contains("commas inside Add()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_HostingBundleMajor_MatchesApiTargetFramework()
    {
        var source = InstallerSource();

        Assert.Contains("MajorVersion", source, StringComparison.Ordinal);
        Assert.Contains($"= {AspNetCoreHostingBundleRequirement.MajorVersion}", source, StringComparison.Ordinal);
        Assert.Contains(AspNetCoreHostingBundleRequirement.TargetFrameworkMoniker, source, StringComparison.Ordinal);
        Assert.Contains("DownloadPage", source, StringComparison.Ordinal);
        Assert.Contains("dotnet.microsoft.com", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_RequiresManualVendorPrerequisiteInstallationAndRedetects()
    {
        var source = InstallerSource();

        Assert.Contains("Wait-ForManualHostingBundleInstallation", source, StringComparison.Ordinal);
        Assert.Contains("Read-Host", source, StringComparison.Ordinal);
        Assert.Contains("ITAdmin will not download or execute this prerequisite", source, StringComparison.Ordinal);
        Assert.Contains("$Unattended.IsPresent", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishPipeline_DoesNotCarryOrDownloadRuntimePrerequisiteInstallers()
    {
        foreach (var source in new[] { PublishScriptSource(), PublishWorkflowSource() })
        {
            Assert.DoesNotContain("acquire-prerequisite", source, StringComparison.Ordinal);
            Assert.DoesNotContain("--prerequisite", source, StringComparison.Ordinal);
        }

        Assert.Contains("Runtime prerequisites do not travel", PublishScriptSource(), StringComparison.Ordinal);
        Assert.Contains("Checking server prerequisites before downloading", BootstrapSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_UsesAnITAdminSshAliasRatherThanCapturingTheRealHost()
    {
        // "Host github.com" would route every GitHub SSH operation this administrator performs
        // through a read-only deploy key scoped to one repository.
        var source = BootstrapSource();

        Assert.Contains(RepositoryAccessContract.SshHostAlias, source, StringComparison.Ordinal);
        Assert.Contains("Resolve-SshHostAlias", source, StringComparison.Ordinal);

        // ...and the alias must be resolved to the real host before anything is persisted for the
        // machine, or the Host Agent breaks when that profile goes away.
        Assert.Contains("$machineRepository", source, StringComparison.Ordinal);
        Assert.Contains("Save-HostAgentSettings -Repository $machineRepository", source, StringComparison.Ordinal);
        Assert.Contains("Install-MachineKnownHosts -Repository $machineRepository", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ResolvesHttpBindingOwnershipBeforeAnyMachineChange()
    {
        // Discovering a port-80 conflict after the site exists surfaces as an opaque "site failed
        // to start"; discovering it in preflight costs the operator one flag and a re-run.
        var source = InstallerSource();

        Assert.Contains("Resolve-HttpBindingOwnership", source, StringComparison.Ordinal);
        Assert.Contains("Get-ExistingWebSiteBindings", source, StringComparison.Ordinal);

        var ownership = source.IndexOf("$bindingOwnership = Resolve-HttpBindingOwnership", StringComparison.Ordinal);
        var stage = source.IndexOf("$releasePaths = Publish-StagedRelease", StringComparison.Ordinal);
        Assert.True(ownership > 0 && stage > 0);
        Assert.True(ownership < stage, "Binding ownership must be resolved before anything is staged.");
    }

    [Fact]
    public void Installer_NeverTouchesASiteItDidNotCreate()
    {
        var source = InstallerSource();

        // The only site the installer may stand down is a pristine Default Web Site, and only when
        // it recorded provisioning IIS itself.
        Assert.Contains("iisProvisionedByInstaller", source, StringComparison.Ordinal);
        Assert.Contains("StandDownPristineDefaultSite", source, StringComparison.Ordinal);
        Assert.Contains("will not stop, rebind, or remove a site it did not create", source, StringComparison.Ordinal);

        // Never deleted - stopping keeps the change reversible.
        Assert.DoesNotContain("Remove-Website", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_NeverSilentlyChoosesADifferentPort()
    {
        // On a production server, quietly moving to a random port is worse than failing.
        var source = InstallerSource();

        Assert.Contains("HTTP binding conflict on port", source, StringComparison.Ordinal);
        Assert.Contains("-HttpPort 8080", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Random", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_RecordsIisProvisioningBeforeTurningItOn()
    {
        // The record has to exist before the Default Web Site does, or a crash in between leaves a
        // machine that cannot tell whose site that is.
        var source = InstallerSource();

        var record = source.IndexOf("Recorded that IIS is being provisioned", StringComparison.Ordinal);
        var install = source.IndexOf("$result = Install-WindowsFeature", StringComparison.Ordinal);
        Assert.True(record > 0 && install > 0);
        Assert.True(record < install, "Provisioning must be recorded before the feature is installed.");
    }

    [Fact]
    public void PrerequisiteAuthority_RemainsWithTheOperatorAndVendor()
    {
        foreach (var source in new[] { PublishScriptSource(), PublishWorkflowSource() })
        {
            Assert.DoesNotContain("acquire-prerequisite", source, StringComparison.Ordinal);
            Assert.DoesNotContain("hosting-bundle.requirement.json", source, StringComparison.Ordinal);
        }

        var installer = InstallerSource();
        Assert.Contains("dotnet.microsoft.com/en-us/download/dotnet/10.0", installer, StringComparison.Ordinal);
        Assert.Contains("manual operator installation", installer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bootstrap_PersistsMachineOwnedRepositoryTrust()
    {
        // The Host Agent runs as LocalSystem and cannot read an administrator's profile - which may
        // also be deleted with the account.
        var source = BootstrapSource();

        Assert.Contains("Install-MachineKnownHosts", source, StringComparison.Ordinal);
        Assert.Contains("known_hosts", source, StringComparison.Ordinal);
        Assert.Contains("UserKnownHostsFile", source, StringComparison.Ordinal);
        Assert.Contains("GlobalKnownHostsFile=/dev/null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_DerivesHostTrustRatherThanInventingIt()
    {
        // ssh-keyscan-and-accept would be exactly the "trust whatever answers" behaviour the
        // documented preparation exists to avoid.
        var source = BootstrapSource();

        Assert.Contains("ssh-keygen -F", source, StringComparison.Ordinal);
        // The comment explaining why keyscan is not used is fine; invoking it is not.
        Assert.DoesNotContain("& ssh-keyscan", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$(ssh-keyscan", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StrictHostKeyChecking=accept-new", source, StringComparison.Ordinal);
        Assert.Contains("will not record a host key it has not seen you verify", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_RecordsInstalledOnlyWhenSomebodyCanLogIn()
    {
        // A worker process answering HTTP 200 is not an installed product when ITAdmin
        // authenticates through LDAP.
        var source = InstallerSource();

        Assert.Contains("Assert-InstallationIsUsable", source, StringComparison.Ordinal);
        Assert.Contains("isSetupRequired", source, StringComparison.Ordinal);
        Assert.Contains("directoryUsable", source, StringComparison.Ordinal);
        Assert.Contains("administratorBootstrapped", source, StringComparison.Ordinal);

        // First occurrence is the call site; the function is declared later in the file.
        var gate = source.IndexOf("    Assert-InstallationIsUsable", StringComparison.Ordinal);
        var installed = source.IndexOf("Set-Phase -State $State -Phase \"Installed\"", StringComparison.Ordinal);
        Assert.True(gate > 0 && installed > 0, "Both the readiness gate and the Installed transition must exist.");
        Assert.True(gate < installed, "Readiness must be asserted before Installed is recorded.");
    }

    [Fact]
    public void Installer_SupportsProvisionThenRePreflightLifecycle()
    {
        var source = InstallerSource();

        Assert.Contains("ProvisionPrerequisites", source, StringComparison.Ordinal);
        Assert.Contains("PrerequisitesOnly", source, StringComparison.Ordinal);
        Assert.Contains("AwaitingReboot", source, StringComparison.Ordinal);
        Assert.Contains("ProvisioningPrerequisites", source, StringComparison.Ordinal);
        Assert.Contains("All required prerequisites confirmed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_UnattendedModeNeverPrompts()
    {
        // The Host Agent invokes the installer with no console; a hidden prompt would hang an
        // update indefinitely instead of failing.
        var source = InstallerSource();

        Assert.Contains("$Unattended.IsPresent", source, StringComparison.Ordinal);
        Assert.Contains("required in unattended mode", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_RegistersTheHostAgentAsASeparatePrivilegedService()
    {
        var source = InstallerSource();

        Assert.Contains("ITAdminHostAgent", source, StringComparison.Ordinal);
        Assert.Contains("LocalSystem", source, StringComparison.Ordinal);
        // The app pool must not be able to modify the privileged binaries or the deployment tooling.
        Assert.Contains("icacls $agentRoot /deny", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_VerifiesEveryDeclaredComponentBeforeInstallingIt()
    {
        // One trust model, not three. The Host Agent runs as LocalSystem and the prerequisite chunks
        // become an executable, so they get exactly the same treatment as the application payload.
        var source = InstallerSource();

        Assert.Contains("Test-ComponentIntegrity", source, StringComparison.Ordinal);
        Assert.Contains("foreach ($component in $manifest.components.PSObject.Properties)", source, StringComparison.Ordinal);
        Assert.Contains("unverified privileged binaries", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_RefusesUndeclaredContentInADistribution()
    {
        // Verifying only declared files would let an extra executable ride along, unverified, next
        // to binaries the installer is about to run as SYSTEM.
        var source = InstallerSource();

        Assert.Contains("Test-DistributionIsClosed", source, StringComparison.Ordinal);
        Assert.Contains("undeclared content", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_ReassemblesAndFullyVerifiesPrerequisitesBeforeExecutingThem()
    {
        // Chunk digests localise a fault; the digest over the REASSEMBLED file is what authorises
        // execution. Individually valid chunks in the wrong order would otherwise reconstruct into
        // something nobody released.
        var source = InstallerSource();

        Assert.Contains("Restore-DistributionPrerequisite", source, StringComparison.Ordinal);
        Assert.Contains("chunkDigests", source, StringComparison.Ordinal);
        Assert.Contains("does not match the digest the", source, StringComparison.Ordinal);
        Assert.Contains("It will not be executed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_InstallsTheHostAgentOutsideTheWebRoot()
    {
        var source = InstallerSource();

        // app/ becomes the IIS physicalPath; the agent must never be installed inside it.
        Assert.Contains($"\"{DeploymentLayout.HostAgentDirectoryName}\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"Join-Path $releasePaths.PayloadRoot \"{DeploymentLayout.HostAgentDirectoryName}\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PublishPipeline_ShipsTheHostAgentWithTheReleaseItManages()
    {
        // An update must never leave a server running an agent from a different version.
        foreach (var source in new[] { PublishScriptSource(), PublishWorkflowSource() })
        {
            Assert.Contains("ITAdmin.HostAgent.csproj", source, StringComparison.Ordinal);
            Assert.Contains("--host-agent", source, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Release publishing
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void PublishPipeline_RefusesLightweightTagsAndPreReleasesOnStable()
    {
        foreach (var source in new[] { PublishScriptSource(), PublishWorkflowSource() })
        {
            Assert.Contains("lightweight tag", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("annotated", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pre-release", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PublishPipeline_BuildsFromThePeeledCommitOfTheTag()
    {
        foreach (var source in new[] { PublishScriptSource(), PublishWorkflowSource() })
        {
            Assert.Contains("^{commit}", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PublishPipeline_VerifiesIdentityBeforePublishing()
    {
        foreach (var source in new[] { PublishScriptSource(), PublishWorkflowSource() })
        {
            Assert.Contains("dist-verify", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PublishPipeline_PublishesToTheDistributionRefNamespace()
    {
        foreach (var source in new[] { PublishScriptSource(), PublishWorkflowSource() })
        {
            Assert.Contains(GitReleaseRefs.DistributionRefPrefix, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PublishScript_DoesNotPushWithoutAnExplicitInstruction() =>
        Assert.Contains("Nothing was pushed", PublishScriptSource(), StringComparison.Ordinal);

    [Fact]
    public void BuildScript_RefusesToMislabelAnArtifactBuiltFromADirtyTree() =>
        Assert.Contains("dirty working tree", BuildScriptSource(), StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------------------------------
    // Application-side contract alignment
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ApiMachineSecretsFileName_MatchesDeploymentContract() =>
        Assert.Equal(
            MachineSecrets.ProtectedFileName,
            ITAdmin.Api.Configuration.MachineSecretsConfigurationExtensions.ProtectedFileName);

    [Fact]
    public void ApiSetupKeyHashConfigurationKey_MatchesTheApplicationsValidator() =>
        Assert.Equal(
            ITAdmin.Application.Common.Security.SetupKeyHashValidator.ConfigurationKey,
            ITAdmin.Api.Configuration.MachineSecretsConfigurationExtensions.SetupKeyHashConfigurationKey);

    [Fact]
    public void HostAgentSettingsFileName_IsWrittenByTheBootstrap() =>
        Assert.Contains(HostAgentSettings.FileName, BootstrapSource(), StringComparison.Ordinal);

    // ------------------------------------------------------------------------------------------
    // Environment neutrality
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void DeploymentScripts_ContainNoAcceptanceEnvironmentValues()
    {
        // The acceptance environment is runtime input, never a product, installer, or script
        // default. The same payload and the same installer must serve any customer.
        string[] acceptanceValues =
        [
            "muglabb", "mugla.bel.tr", "SRV-ITADMIN",
            "10.5.1.", "10.30.40.", "DC=muglabb",
        ];

        foreach (var (name, source) in new[]
                 {
                     ("Install-ITAdmin.ps1", InstallerSource()),
                     ("Bootstrap-ITAdmin.ps1", BootstrapSource()),
                     ("Test-ItAdminAdReadiness.ps1", ReadinessScriptSource()),
                     ("build-release.zsh", BuildScriptSource()),
                     ("publish-release.zsh", PublishScriptSource()),
                     ("publish-release.yml", PublishWorkflowSource()),
                 })
        {
            foreach (var value in acceptanceValues)
            {
                Assert.False(
                    source.Contains(value, StringComparison.OrdinalIgnoreCase),
                    $"{name} contains the acceptance-environment value '{value}'.");
            }
        }
    }

    [Fact]
    public void DeploymentScripts_HardCodeNoRepositoryOwnerOrName()
    {
        // The repository is discovered from the clone's own origin, so a fork or a mirror works
        // unchanged and the installer can never disagree with the remote the operator keyed.
        var source = BootstrapSource();

        Assert.Contains("remote get-url origin", source, StringComparison.Ordinal);
        Assert.DoesNotContain("github.com/", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git@github.com:", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Installer_DeclaresNoOrganizationSpecificParameterDefault()
    {
        var source = InstallerSource();
        var start = source.IndexOf("\nparam(", StringComparison.Ordinal);
        Assert.True(start > 0, "Could not locate the param() block.");
        var end = source.IndexOf("\n)", start, StringComparison.Ordinal);
        var paramBlock = source[start..end];

        foreach (var parameter in new[]
                 {
                     "$DatabaseHost", "$DatabaseName", "$DatabaseUser",
                     "$DirectoryHost", "$DirectoryBaseDn", "$DirectoryBindUser",
                     "$InitialAdministrator", "$HttpHostHeader",
                 })
        {
            Assert.Contains(parameter, paramBlock, StringComparison.Ordinal);
            Assert.DoesNotContain($"{parameter} =", paramBlock, StringComparison.Ordinal);
        }

        // Technology-standard defaults are fine; organization values never are.
        Assert.Contains("$DatabasePort = 5432", paramBlock, StringComparison.Ordinal);
        Assert.Contains("$HttpPort = 80", paramBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadinessScript_DeclaresNoDefaultForAnySiteSpecificParameter()
    {
        // Only the param() block matters: assigning these at run time from AD discovery is the
        // whole point, but the repository must not presume any value up front.
        var source = ReadinessScriptSource();
        var start = source.IndexOf("\nparam(", StringComparison.Ordinal);
        Assert.True(start > 0, "Could not locate the param() block.");
        var end = source.IndexOf("\n)", start, StringComparison.Ordinal);
        Assert.True(end > start, "Could not locate the end of the param() block.");
        var paramBlock = source[start..end];

        foreach (var parameter in new[]
                 {
                     "$DomainFqdn", "$DomainControllers", "$BaseDn", "$PostgreSqlHost", "$WebHost",
                 })
        {
            Assert.Contains(parameter, paramBlock, StringComparison.Ordinal);
            Assert.DoesNotContain($"{parameter} =", paramBlock, StringComparison.Ordinal);
        }

        // A technology-standard port default is fine; an organization value never is.
        Assert.Contains("$PostgreSqlPort = 5432", paramBlock, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------------------------
    // Windows PowerShell 5.1 compatibility
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void WindowsTargetedPowerShell_AvoidsTypographicPunctuationThatBreaksPs51Decoding()
    {
        // Windows PowerShell 5.1 reads .ps1 files through the legacy ANSI code page when there is
        // no BOM. UTF-8 em dashes / smart quotes become mojibake that can introduce a typographic
        // quote byte and prematurely terminate strings. Keep executable Windows deployment scripts
        // on plain ASCII punctuation.
        char[] unsafePunctuation =
        [
            '—', // em dash
            '–', // en dash
            '‘', // left single quotation mark
            '’', // right single quotation mark
            '“', // left double quotation mark
            '”', // right double quotation mark
            '…', // horizontal ellipsis
        ];

        var root = RepositoryRoot();

        // Every executable Windows PowerShell source in the repository, discovered rather than
        // listed, so a new script cannot quietly escape this rule.
        var targets = Directory
            .GetFiles(Path.Combine(root, "scripts"), "*.ps1", SearchOption.AllDirectories)
            .ToList();

        Assert.NotEmpty(targets);
        Assert.Contains(targets, path => path.EndsWith("Bootstrap-ITAdmin.ps1", StringComparison.Ordinal));
        Assert.Contains(targets, path => path.EndsWith("Install-ITAdmin.ps1", StringComparison.Ordinal));

        foreach (var path in targets)
        {
            var source = File.ReadAllText(path);
            foreach (var ch in unsafePunctuation)
            {
                Assert.False(
                    source.Contains(ch),
                    $"{Path.GetRelativePath(root, path)} contains U+{(int)ch:X4} typographic punctuation; " +
                    "use plain ASCII in Windows PowerShell 5.1 sources.");
            }
        }
    }
}
