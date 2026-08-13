using System.Text.Json;
using ITAdmin.Deployment;

namespace ITAdmin.UnitTests.Deployment;

public sealed class IisPrerequisiteFeaturesTests
{
    [Fact]
    public void Required_ContainsExactlyTheAuthoritativeFeatureSet()
    {
        var names = IisPrerequisiteFeatures.RequiredNames;

        Assert.Equal(
            [
                "Web-Server",
                "Web-Default-Doc",
                "Web-Http-Errors",
                "Web-Static-Content",
                "Web-Http-Logging",
                "Web-Stat-Compression",
                "Web-Filtering",
                "Web-Mgmt-Console",
                "Web-Scripting-Tools",
            ],
            names);
    }

    [Fact]
    public void Required_DoesNotOpenUnnecessaryIisSurface()
    {
        foreach (var name in IisPrerequisiteFeatures.RequiredNames)
        {
            Assert.DoesNotContain("Web-Asp-Net", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Web-Net-Ext", name, StringComparison.OrdinalIgnoreCase);
            Assert.False(name.Equals("Web-Dir-Browsing", StringComparison.OrdinalIgnoreCase));
            Assert.False(name.Equals("Web-DAV-Publishing", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Required_EveryFeatureHasAReason()
    {
        Assert.All(IisPrerequisiteFeatures.Required, feature =>
        {
            Assert.False(string.IsNullOrWhiteSpace(feature.Name));
            Assert.False(string.IsNullOrWhiteSpace(feature.Reason));
        });
    }
}

public sealed class AspNetCoreHostingBundleRequirementTests
{
    [Fact]
    public void MajorVersion_MatchesApiTargetFramework()
    {
        var csproj = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "backend", "src", "ITAdmin.Api", "ITAdmin.Api.csproj"));

        Assert.Contains($"<TargetFramework>{AspNetCoreHostingBundleRequirement.TargetFrameworkMoniker}</TargetFramework>", csproj, StringComparison.Ordinal);
        Assert.Contains($"net{AspNetCoreHostingBundleRequirement.MajorVersion}.", AspNetCoreHostingBundleRequirement.TargetFrameworkMoniker, StringComparison.Ordinal);
    }

    [Fact]
    public void RequirementMetadataFile_MatchesConstants()
    {
        var path = Path.Combine(FindRepositoryRoot(), AspNetCoreHostingBundleRequirement.RequirementMetadataRelativePath);
        Assert.True(File.Exists(path), $"Missing {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal(AspNetCoreHostingBundleRequirement.MajorVersion, root.GetProperty("majorVersion").GetInt32());
        Assert.Equal(AspNetCoreHostingBundleRequirement.MinimumVersion, root.GetProperty("minimumVersion").GetString());
        Assert.Equal(AspNetCoreHostingBundleRequirement.TargetFrameworkMoniker, root.GetProperty("targetFramework").GetString());
        Assert.Equal(AspNetCoreHostingBundleRequirement.InstallerFileNamePattern, root.GetProperty("installerFileNamePattern").GetString());
        Assert.Equal(AspNetCoreHostingBundleRequirement.AncmModuleName, root.GetProperty("ancmModuleName").GetString());
    }

    [Fact]
    public void IsHostingUsable_WhenAncmSharedFrameworkAndModulePresent_IsTrue() =>
        Assert.True(AspNetCoreHostingBundleRequirement.IsHostingUsable(
            ancmPresent: true,
            sharedFrameworkSatisfied: true,
            iisInstalled: true,
            webAdministrationAvailable: true,
            ancmModuleRegistered: true));

    [Fact]
    public void IsHostingUsable_WhenSharedFrameworkMissing_IsFalse() =>
        Assert.False(AspNetCoreHostingBundleRequirement.IsHostingUsable(
            ancmPresent: true,
            sharedFrameworkSatisfied: false,
            iisInstalled: true,
            webAdministrationAvailable: true,
            ancmModuleRegistered: true));

    [Fact]
    public void IsHostingUsable_WhenAncmMissing_IsFalse() =>
        Assert.False(AspNetCoreHostingBundleRequirement.IsHostingUsable(
            ancmPresent: false,
            sharedFrameworkSatisfied: true,
            iisInstalled: true,
            webAdministrationAvailable: true,
            ancmModuleRegistered: false));

    [Fact]
    public void IsHostingUsable_WhenIisModuleNotRegistered_IsFalse() =>
        Assert.False(AspNetCoreHostingBundleRequirement.IsHostingUsable(
            ancmPresent: true,
            sharedFrameworkSatisfied: true,
            iisInstalled: true,
            webAdministrationAvailable: true,
            ancmModuleRegistered: false));

    [Fact]
    public void ShouldRepairHostingBundle_WhenBitsPresentButModuleMissing() =>
        Assert.True(AspNetCoreHostingBundleRequirement.ShouldRepairHostingBundle(
            ancmPresent: true,
            sharedFrameworkSatisfied: true,
            ancmModuleRegistered: false));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}

public sealed class HostingBundleDetectionSemanticsTests
{
    /// <summary>
    /// Mirrors the SRV-ITADMIN acceptance observation: ANCM file version 20.x with AspNetCore.App
    /// 10.x and moduleRegistered=true must be usable. ANCM major must not be compared to TFM 10.
    /// </summary>
    [Fact]
    public void RealWorldAncmFileVersion20_WithNet10SharedFramework_IsUsable()
    {
        var detection = new PrerequisiteDetection(
            MissingIisFeatures: Array.Empty<string>(),
            WebAdministrationAvailable: true,
            AncmPresent: true,
            SharedFrameworkSatisfied: true,
            AncmModuleRegistered: true,
            RestartPending: false,
            IisInstalled: true);

        Assert.True(detection.HostingBundleUsable);
        Assert.False(detection.HostingBundleMissingOrUnusable);
        Assert.False(detection.HasBlockingGaps);
        Assert.Empty(detection.FormatHostingBlockingProblems(
            AspNetCoreHostingBundleRequirement.DisplayName,
            AspNetCoreHostingBundleRequirement.InstallerFileNamePattern,
            AspNetCoreHostingBundleRequirement.AncmModuleName,
            ancmVersionText: "20.0.26177.10"));
    }

    [Fact]
    public void SharedFrameworkMissing_IsNotUsableEvenWhenAncmPresent()
    {
        var detection = new PrerequisiteDetection(
            Array.Empty<string>(), true, AncmPresent: true, SharedFrameworkSatisfied: false,
            AncmModuleRegistered: true, RestartPending: false);

        Assert.False(detection.HostingBundleUsable);
        var problems = detection.FormatHostingBlockingProblems(
            AspNetCoreHostingBundleRequirement.DisplayName,
            AspNetCoreHostingBundleRequirement.InstallerFileNamePattern,
            AspNetCoreHostingBundleRequirement.AncmModuleName,
            "20.0.26177.10");
        Assert.Contains(problems, p => p.Contains("Microsoft.AspNetCore.App", StringComparison.Ordinal));
    }

    [Fact]
    public void AncmMissing_IsNotUsable()
    {
        var detection = new PrerequisiteDetection(
            Array.Empty<string>(), true, AncmPresent: false, SharedFrameworkSatisfied: false,
            AncmModuleRegistered: false, RestartPending: false);

        Assert.False(detection.HostingBundleUsable);
        var problems = detection.FormatHostingBlockingProblems(
            AspNetCoreHostingBundleRequirement.DisplayName,
            AspNetCoreHostingBundleRequirement.InstallerFileNamePattern,
            AspNetCoreHostingBundleRequirement.AncmModuleName);
        Assert.Contains(problems, p => p.Contains("ASP.NET Core Module V2 was not found", StringComparison.Ordinal));
    }

    [Fact]
    public void ModuleRegistrationMissing_IsNotUsable_AndSuggestsRepair()
    {
        var detection = new PrerequisiteDetection(
            Array.Empty<string>(), true, AncmPresent: true, SharedFrameworkSatisfied: true,
            AncmModuleRegistered: false, RestartPending: false);

        Assert.False(detection.HostingBundleUsable);
        Assert.True(AspNetCoreHostingBundleRequirement.ShouldRepairHostingBundle(true, true, false));
        var problems = detection.FormatHostingBlockingProblems(
            AspNetCoreHostingBundleRequirement.DisplayName,
            AspNetCoreHostingBundleRequirement.InstallerFileNamePattern,
            AspNetCoreHostingBundleRequirement.AncmModuleName,
            "20.0.26177.10");
        Assert.Contains(problems, p => p.Contains("not registered with IIS", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("20.0.26177.10")]
    [InlineData("13.1.20317.16")]
    [InlineData("10.0.0")]
    public void FormatHostingBlockingProblems_NeverThrows_ForAncmVersionText(string? ancmVersion)
    {
        var detection = new PrerequisiteDetection(
            Array.Empty<string>(), true, true, true, false, false);

        var ex = Record.Exception(() => detection.FormatHostingBlockingProblems(
            AspNetCoreHostingBundleRequirement.DisplayName,
            AspNetCoreHostingBundleRequirement.InstallerFileNamePattern,
            AspNetCoreHostingBundleRequirement.AncmModuleName,
            ancmVersion));

        Assert.Null(ex);
        Assert.NotEmpty(detection.FormatHostingDetectionDetail());
    }

    [Fact]
    public void AlreadyHealthy_PlanDoesNotRequestReinstall()
    {
        var detection = new PrerequisiteDetection(
            Array.Empty<string>(), true, true, true, true, false);

        var plan = PrerequisiteLifecycle.Plan(detection, provisionRequested: true);

        Assert.Equal(PrerequisitePlanAction.Continue, plan.Action);
        Assert.False(plan.RequiresHostingBundleInstall);
        Assert.Empty(plan.FeaturesToInstall);
    }

    [Fact]
    public void FailedPrerequisiteRecovery_WhenHealthy_ContinuesWithoutReinstall()
    {
        // Equivalent to rerunning -ProvisionPrerequisites -PrerequisitesOnly after Failed at
        // ProvisionHostingBundle once detection semantics are corrected.
        var detection = new PrerequisiteDetection(
            Array.Empty<string>(), true, true, true, true, false);

        Assert.False(detection.HasBlockingGaps);
        var plan = PrerequisiteLifecycle.Plan(detection, provisionRequested: true);
        Assert.Equal(PrerequisitePlanAction.Continue, plan.Action);
    }
}

public sealed class PrerequisiteLifecycleTests
{
    private static PrerequisiteDetection Gaps(
        IReadOnlyList<string>? missingFeatures = null,
        bool webAdmin = true,
        bool ancmPresent = true,
        bool sharedFrameworkOk = true,
        bool ancmRegistered = true,
        bool restartPending = false,
        bool iisInstalled = true) =>
        new(
            missingFeatures ?? Array.Empty<string>(),
            webAdmin,
            ancmPresent,
            sharedFrameworkOk,
            ancmRegistered,
            restartPending,
            iisInstalled);

    [Fact]
    public void Plan_WhenReady_Continues()
    {
        var plan = PrerequisiteLifecycle.Plan(Gaps(), provisionRequested: false);

        Assert.Equal(PrerequisitePlanAction.Continue, plan.Action);
        Assert.False(plan.RequiresRebootBeforeContinue);
    }

    [Fact]
    public void Plan_WhenGapsAndProvisionNotRequested_FailsReportOnly()
    {
        var plan = PrerequisiteLifecycle.Plan(
            Gaps(missingFeatures: ["Web-Server"], ancmPresent: false, sharedFrameworkOk: false, ancmRegistered: false, webAdmin: false, iisInstalled: false),
            provisionRequested: false);

        Assert.Equal(PrerequisitePlanAction.FailReportOnly, plan.Action);
        Assert.Contains("Web-Server", plan.FeaturesToInstall);
        Assert.True(plan.RequiresHostingBundleInstall);
    }

    [Fact]
    public void Plan_WhenGapsAndProvisionRequested_InstallsIisFirst()
    {
        var plan = PrerequisiteLifecycle.Plan(
            Gaps(missingFeatures: ["Web-Server", "Web-Static-Content"], ancmPresent: false, sharedFrameworkOk: false, ancmRegistered: false, iisInstalled: false),
            provisionRequested: true);

        Assert.Equal(PrerequisitePlanAction.InstallIisFeatures, plan.Action);
        Assert.Equal(["Web-Server", "Web-Static-Content"], plan.FeaturesToInstall);
        Assert.False(plan.RequiresHostingBundleInstall);
    }

    [Fact]
    public void Plan_WhenIisReadyButHostingMissing_InstallsHostingBundle()
    {
        var plan = PrerequisiteLifecycle.Plan(
            Gaps(ancmPresent: false, sharedFrameworkOk: false, ancmRegistered: false),
            provisionRequested: true);

        Assert.Equal(PrerequisitePlanAction.InstallHostingBundle, plan.Action);
        Assert.True(plan.RequiresHostingBundleInstall);
    }

    [Fact]
    public void Plan_WhenAncmNotRegistered_TreatsHostingAsUnusable()
    {
        var detection = Gaps(ancmRegistered: false);
        Assert.True(detection.HostingBundleMissingOrUnusable);

        var plan = PrerequisiteLifecycle.Plan(detection, provisionRequested: true);
        Assert.Equal(PrerequisitePlanAction.InstallHostingBundle, plan.Action);
    }

    [Fact]
    public void Plan_WhenRestartPending_StopsForRebootEvenIfProvisionRequested()
    {
        var plan = PrerequisiteLifecycle.Plan(Gaps(restartPending: true), provisionRequested: true);

        Assert.Equal(PrerequisitePlanAction.StopForReboot, plan.Action);
        Assert.True(plan.RequiresRebootBeforeContinue);
    }

    [Fact]
    public void InterpretProvisionResult_RestartNeeded_StopsForReboot()
    {
        var plan = PrerequisiteLifecycle.InterpretProvisionResult(
            new PrerequisiteProvisionResult(true, RestartNeeded: true, "reboot required"));

        Assert.Equal(PrerequisitePlanAction.StopForReboot, plan.Action);
    }

    [Fact]
    public void InterpretProvisionResult_Success_RequestsReDetect()
    {
        var plan = PrerequisiteLifecycle.InterpretProvisionResult(
            new PrerequisiteProvisionResult(true, RestartNeeded: false, "ok"));

        Assert.Equal(PrerequisitePlanAction.ReDetect, plan.Action);
    }

    [Fact]
    public void InterpretProvisionResult_Failure_FailsReportOnly()
    {
        var plan = PrerequisiteLifecycle.InterpretProvisionResult(
            new PrerequisiteProvisionResult(false, RestartNeeded: false, "failed"));

        Assert.Equal(PrerequisitePlanAction.FailReportOnly, plan.Action);
    }
}

public sealed class MachineSecretsTests
{
    [Fact]
    public void ProtectedFileName_IsStableContract() =>
        Assert.Equal("runtime.secrets.dpapi", MachineSecrets.ProtectedFileName);

    [Fact]
    public void Json_RoundTripsSecretFields()
    {
        var secrets = new MachineSecrets
        {
            ConnectionString = "Host=db;Password=s3cret",
            JwtKey = "jwt-material",
        };

        var restored = MachineSecrets.FromJson(secrets.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(secrets.ConnectionString, restored!.ConnectionString);
        Assert.Equal(secrets.JwtKey, restored.JwtKey);
    }

    [Fact]
    public void Validate_RejectsMissingFields()
    {
        var result = new MachineSecrets().Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("connectionString", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("jwtKey", StringComparison.Ordinal));
    }
}
