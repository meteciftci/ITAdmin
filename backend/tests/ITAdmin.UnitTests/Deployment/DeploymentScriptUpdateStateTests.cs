using System.Runtime.CompilerServices;
using System.Management.Automation.Language;

namespace ITAdmin.UnitTests.Deployment;

public sealed class DeploymentScriptUpdateStateTests
{
    [Fact]
    public void DeploymentScript_RepairsTheProtectedStateAclAndHasValidSyntax()
    {
        var script = File.ReadAllText(DeploymentScriptPath());

        Parser.ParseInput(script, out _, out var errors);

        Assert.Empty(errors);
        Assert.Contains("function Set-StateDirectoryPermissions", script, StringComparison.Ordinal);
        Assert.Contains("S-1-5-18", script, StringComparison.Ordinal);
        Assert.Contains("S-1-5-32-544", script, StringComparison.Ordinal);
        Assert.Contains("Set-StateDirectoryPermissions", script, StringComparison.Ordinal);
    }

    private static string DeploymentScriptPath([CallerFilePath] string callerFilePath = "")
    {
        var root = Directory.GetParent(Path.GetDirectoryName(callerFilePath)!)!.Parent!.Parent!.Parent!.FullName;
        return Path.Combine(root, "scripts", "deploy", "Deploy-ITAdmin.ps1");
    }
}
