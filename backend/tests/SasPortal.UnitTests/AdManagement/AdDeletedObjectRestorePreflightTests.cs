using System.DirectoryServices.Protocols;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Infrastructure.Ldap;
using SasPortal.Infrastructure.Services;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdLdapNoSuchObjectHelperTests
{
    [Fact]
    public void IsLdapNoSuchObject_ReturnsTrue_ForErrorCode32()
    {
        var exception = new LdapException(32, "No such object");

        Assert.True(AdLdapNoSuchObjectHelper.IsLdapNoSuchObject(exception));
    }

    [Fact]
    public void IsLdapNoSuchObject_ReturnsFalse_ForOtherErrorCodes()
    {
        var exception = new LdapException(81, "Server unavailable");

        Assert.False(AdLdapNoSuchObjectHelper.IsLdapNoSuchObject(exception));
    }

    [Fact]
    public void IsNoSuchObjectResultCode_ReturnsTrue_ForNoSuchObject()
    {
        Assert.True(AdLdapNoSuchObjectHelper.IsNoSuchObjectResultCode(ResultCode.NoSuchObject));
    }

    [Fact]
    public void IsNoSuchObjectResultCode_ReturnsFalse_ForSuccess()
    {
        Assert.False(AdLdapNoSuchObjectHelper.IsNoSuchObjectResultCode(ResultCode.Success));
    }
}

public sealed class AdDeletedObjectRestorePreflightTests
{
    [Fact]
    public void DeletedObjectRestoreService_HandlesNoSuchObjectInBaseDnSearchHelpers()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("TrySendBaseDnSearch", source, StringComparison.Ordinal);
        Assert.Contains("AdLdapNoSuchObjectHelper.IsDirectoryNoSuchObject", source, StringComparison.Ordinal);
        Assert.Contains("AdLdapNoSuchObjectHelper.IsLdapNoSuchObject", source, StringComparison.Ordinal);
        Assert.Contains("AdLdapNoSuchObjectHelper.IsNoSuchObjectResultCode", source, StringComparison.Ordinal);
        Assert.Contains("TryLoadDirectoryObjectByDn(ldapConnection, restoredDistinguishedName)", source, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestoreSteps.CheckConflict", source, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestoreSteps.CheckParentExists", source, StringComparison.Ordinal);
        Assert.Contains("deletedObjectRestoreCommandRunner.ExecuteRestoreAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_ParentCheckReturnsControlledFailureWhenParentMissing()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("AdManagementApiMessageKeys.DeletedObjects.RestoreParentNotFound", source, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestoreSteps.CheckParentExists", source, StringComparison.Ordinal);
        Assert.Contains("The restore target parent could not be found.", source, StringComparison.Ordinal);
        Assert.Contains("The restore target OU could not be found.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestorePowerShellFailureDiagnostic_ExcludesLdapCodes()
    {
        var diagnosticJson = AdOperationErrorDiagnosticBuilder.BuildDeletedObjectRestoreFailureJson(
            "RestoreObject",
            Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            "CN=deleted,CN=Deleted Objects,DC=example,DC=com",
            "CN=test,OU=Users,DC=example,DC=com",
            "PowerShellRestoreAdObject",
            englishMessageOverride:
                "AD restore komutu çalıştırılamadı. Active Directory PowerShell modülü sunucuda bulunamadı.",
            normalizedReasonOverride: "ConnectionFailed",
            command: "Restore-ADObject",
            restoreTargetMode: "OriginalLocation",
            server: "dc1.example.com",
            sanitizedPowerShellError: AdDeletedObjectRestorePowerShellCommandRunner.ModuleMissingErrorToken,
            powerShellExitCode: 1,
            elapsedMs: 42,
            credentialMode: "ServiceAccount");

        Assert.Contains("\"restoreOperationMode\":\"PowerShellRestoreAdObject\"", diagnosticJson, StringComparison.Ordinal);
        Assert.Contains("\"command\":\"Restore-ADObject\"", diagnosticJson, StringComparison.Ordinal);
        Assert.Contains(
            $"\"sanitizedPowerShellError\":\"{AdDeletedObjectRestorePowerShellCommandRunner.ModuleMissingErrorToken}\"",
            diagnosticJson,
            StringComparison.Ordinal);
        Assert.Contains("\"credentialMode\":\"ServiceAccount\"", diagnosticJson, StringComparison.Ordinal);
        Assert.Contains("\"elapsedMs\":42", diagnosticJson, StringComparison.Ordinal);
        Assert.Contains("\"powerShellExitCode\":1", diagnosticJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ldapResultCode\":", diagnosticJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ldapExceptionErrorCode\":", diagnosticJson, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestorePowerShellRunner_ReportsModuleMissingToken()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdDeletedObjectRestorePowerShellCommandRunner.cs"));

        Assert.Contains("ModuleMissingErrorToken", source, StringComparison.Ordinal);
        Assert.Contains("ActiveDirectoryModuleNotFound", source, StringComparison.Ordinal);
        Assert.Contains("Get-Module -ListAvailable", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_ResolvesModuleMissingUserMessage()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("AdManagementApiMessageKeys.DeletedObjects.RestorePowerShellModuleMissing", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementApiMessages.Legacy(AdManagementApiMessageKeys.DeletedObjects.RestorePowerShellModuleMissing)", source, StringComparison.Ordinal);
        Assert.Contains("ResolveDeletedObjectRestorePowerShellFailureMessage", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreReadinessPowerShellProbe_RecycleBinChecksIdentityAndEnabledScopes()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdDeletedObjectRestoreReadinessPowerShellProbe.cs"));

        Assert.Contains("Get-ADOptionalFeature", source, StringComparison.Ordinal);
        Assert.Contains("-Identity 'Recycle Bin Feature'", source, StringComparison.Ordinal);
        Assert.Contains("-Properties EnabledScopes", source, StringComparison.Ordinal);
        Assert.Contains("EnabledScopes.Count", source, StringComparison.Ordinal);
        Assert.Contains("EnabledScopes.Count -le 0", source, StringComparison.Ordinal);
        Assert.Contains("RecycleBinFeatureDisabled", source, StringComparison.Ordinal);

        Assert.Contains(
            "-Credential $credential",
            source,
            StringComparison.Ordinal);

        Assert.DoesNotContain("-Filter 'name -eq", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-not $feature.Enabled", source, StringComparison.Ordinal);

        Assert.Contains(
            "New-Object System.Management.Automation.PSCredential($bindIdentity, $securePassword)",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend", "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved.");
    }
}
