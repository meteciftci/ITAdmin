namespace ITAdmin.Deployment;

/// <summary>
/// Which ASP.NET Core Hosting Bundle the Windows host must have. Derived from the API project's
/// <c>TargetFramework</c> - never guessed from a prompt or a changelog.
///
/// <para>
/// The Hosting Bundle is a Microsoft redistributable (~100MB+). It is never committed to the
/// repository. The installer consumes an operator-supplied offline installer whose integrity is
/// verified before it is executed.
/// </para>
///
/// <para>
/// Versioning note: the required shared framework major (<see cref="MajorVersion"/>) comes from
/// the app TFM (net10.0 → 10). The ASP.NET Core Module (ANCM) ships with its own file/product
/// version (observed as 20.x on current Hosting Bundles) and must not be compared to the TFM
/// major. Hosting readiness is ANCM present + AspNetCore.App shared framework satisfied + IIS
/// module registration when IIS is available.
/// </para>
/// </summary>
public static class AspNetCoreHostingBundleRequirement
{
    /// <summary>
    /// Must match <c>ITAdmin.Api.csproj</c> <c>TargetFramework</c> major (net10.0 → 10).
    /// This is the <c>Microsoft.AspNetCore.App</c> shared-framework major, not the ANCM DLL
    /// file-version major. Drift-tested against the csproj.
    /// </summary>
    public const int MajorVersion = 10;

    public const string TargetFrameworkMoniker = "net10.0";

    public const string MinimumVersion = "10.0.0";

    public const string DisplayName = "Microsoft ASP.NET Core 10.0 Hosting Bundle";

    /// <summary>Relative to <c>%ProgramFiles%</c>.</summary>
    public const string AncmRelativePath = @"IIS\Asp.Net Core Module\V2\aspnetcorev2.dll";

    public const string AncmModuleName = "AspNetCoreModuleV2";

    /// <summary>
    /// Filename glob operators use when placing the offline installer next to the ITAdmin
    /// installer or release artifact. The digits after the major must match a real Microsoft
    /// build; the installer verifies the file hash rather than trusting the name alone.
    /// </summary>
    public const string InstallerFileNamePattern = "dotnet-hosting-10.*.exe";

    /// <summary>
    /// Repository-relative path of the checked-in requirement metadata (no binary).
    /// </summary>
    public const string RequirementMetadataRelativePath =
        @"scripts/install/prerequisites/hosting-bundle.requirement.json";

    /// <summary>
    /// MSI/EXE exit code Microsoft installers use for "success, reboot required".
    /// </summary>
    public const int SuccessRebootRequiredExitCode = 3010;

    /// <summary>
    /// Pure hosting readiness from independently verified signals. Does not consult ANCM
    /// file-version major - that number is not the TFM major.
    /// </summary>
    public static bool IsHostingUsable(
        bool ancmPresent,
        bool sharedFrameworkSatisfied,
        bool iisInstalled,
        bool webAdministrationAvailable,
        bool ancmModuleRegistered)
    {
        if (!ancmPresent || !sharedFrameworkSatisfied)
        {
            return false;
        }

        // When IIS management is available, ANCM must be registered with IIS. A DLL left from a
        // Hosting Bundle install that happened before IIS is not enough.
        if (iisInstalled && webAdministrationAvailable && !ancmModuleRegistered)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether a Hosting Bundle (re)install should use <c>/repair</c> rather than a fresh
    /// <c>/install</c>: ANCM bits and the required shared framework are present, but IIS does
    /// not yet show the module (typical when IIS was enabled after the bundle).
    /// </summary>
    public static bool ShouldRepairHostingBundle(
        bool ancmPresent,
        bool sharedFrameworkSatisfied,
        bool ancmModuleRegistered) =>
        ancmPresent && sharedFrameworkSatisfied && !ancmModuleRegistered;
}

/// <summary>
/// Checked-in offline contract for the Hosting Bundle. The binary itself is never in git; this
/// metadata tells the installer (and drift tests) which shared-framework major/minimum to demand.
/// </summary>
public sealed record HostingBundleRequirementMetadata
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string Product { get; init; } = "ASP.NET Core Hosting Bundle";

    public int MajorVersion { get; init; } = AspNetCoreHostingBundleRequirement.MajorVersion;

    public string MinimumVersion { get; init; } = AspNetCoreHostingBundleRequirement.MinimumVersion;

    public string TargetFramework { get; init; } = AspNetCoreHostingBundleRequirement.TargetFrameworkMoniker;

    public string InstallerFileNamePattern { get; init; } =
        AspNetCoreHostingBundleRequirement.InstallerFileNamePattern;

    public string AncmRelativePath { get; init; } = AspNetCoreHostingBundleRequirement.AncmRelativePath;

    public string AncmModuleName { get; init; } = AspNetCoreHostingBundleRequirement.AncmModuleName;
}
