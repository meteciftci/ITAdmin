namespace ITAdmin.UnitTests.Deployment;

public sealed class BootstrapWindowsRegressionTests
{
    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(callerFilePath)!);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static string BootstrapSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "scripts", "install", "Bootstrap-ITAdmin.ps1"));

    [Fact]
    public void InvokeGit_DoesNotTreatWindowsPowerShellNativeStderrAsATerminatingError()
    {
        var source = BootstrapSource();
        var functionStart = source.IndexOf("function Invoke-Git", StringComparison.Ordinal);
        var nextFunction = source.IndexOf("function Get-RepositoryAccessDiagnosis", functionStart, StringComparison.Ordinal);
        var invokeGit = source[functionStart..nextFunction];

        Assert.Contains("$previousErrorActionPreference = $ErrorActionPreference", invokeGit, StringComparison.Ordinal);
        Assert.Contains("$ErrorActionPreference = \"Continue\"", invokeGit, StringComparison.Ordinal);
        Assert.Contains("$exitCode = $LASTEXITCODE", invokeGit, StringComparison.Ordinal);
        Assert.Contains("$ErrorActionPreference = $previousErrorActionPreference", invokeGit, StringComparison.Ordinal);
        Assert.Contains("2>&1", invokeGit, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshInstall_CreatesTheAppPoolIdentityBeforeFetchingThePayload()
    {
        var source = BootstrapSource();
        var initialize = source.IndexOf("Initialize-BootstrapAppPoolIdentity", StringComparison.Ordinal);
        var fetch = source.LastIndexOf("Get-ReleasePayload -Repository", StringComparison.Ordinal);

        Assert.True(initialize >= 0, "Bootstrap must provision the IIS virtual account on a fresh host.");
        Assert.True(fetch > initialize, "The app-pool identity must exist before release staging can apply ACLs.");
        Assert.Contains("Set-DeploymentToolingAppPoolDenyAcl", source, StringComparison.Ordinal);
        Assert.Contains("AllowMissingIdentity", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolingAcl_IsFailClosedOnceTheAppPoolIdentityExists()
    {
        var source = BootstrapSource();

        Assert.Contains("Failed to deny write access", source, StringComparison.Ordinal);
        Assert.Contains("if ($LASTEXITCODE -ne 0)", source, StringComparison.Ordinal);
        Assert.Contains("Application pool identity ready; deployment tooling is write-protected", source, StringComparison.Ordinal);
    }
}
