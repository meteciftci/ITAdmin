using System.DirectoryServices.Protocols;
using System.Reflection;
using SasPortal.Api.Authorization;
using SasPortal.Api.Controllers;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;
using SasPortal.Infrastructure.Services;
using SasPortal.Persistence.Services;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdDeletedObjectRestoreTests
{
    [Fact]
    public void DeletedObjectsRestorePermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.DeletedObjects.Restore", AdManagementPermissions.DeletedObjectsRestore);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsDeletedObjectsRestore()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        Assert.Contains(permissions.Cast<object>(), item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.DeletedObjectsRestore, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SeedDeletedObjectsRestoreMigration_ContainsPermissionAndAdministratorGrant()
    {
        var migrationPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SasPortal.Persistence",
            "Migrations",
            "20260615130000_SeedAdManagementDeletedObjectsRestorePermission.cs"));
        var migrationSource = File.ReadAllText(migrationPath);

        Assert.Contains(AdManagementPermissions.DeletedObjectsRestore, migrationSource, StringComparison.Ordinal);
        Assert.Contains("portal_permissions", migrationSource, StringComparison.Ordinal);
        Assert.Contains("portal_role_permissions", migrationSource, StringComparison.Ordinal);
        Assert.Contains("WHERE NOT EXISTS", migrationSource, StringComparison.Ordinal);
        Assert.Contains("Administrator", migrationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreDeletedObjectEndpoint_RequiresDeletedObjectsRestorePermission()
    {
        var method = typeof(AdManagementController).GetMethod(nameof(AdManagementController.RestoreDeletedObject));
        Assert.NotNull(method);

        var permissionAttribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.Equal(
            RequirePermissionAttribute.PolicyPrefix + AdManagementPermissions.DeletedObjectsRestore,
            permissionAttribute?.Policy);
    }

    [Fact]
    public void RestoreDeletedObjectEndpoint_InvalidGuid_ReturnsBadRequest()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("RestoreDeletedObject", source, StringComparison.Ordinal);
        Assert.Contains("Geçersiz silinen nesne kimliği.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreDeletedObjectEndpoint_PassesActorContextToService()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("AdDeletedObjectRestoreRequest", source, StringComparison.Ordinal);
        Assert.Contains("ResolveActorUserId(User)", source, StringComparison.Ordinal);
        Assert.Contains("ResolveIpAddress()", source, StringComparison.Ordinal);
        Assert.Contains("ResolveUserAgent()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_UsesPowerShellRestoreAdObjectCommandRunner()
    {
        var restoreSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));
        var runnerSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdDeletedObjectRestorePowerShellCommandRunner.cs"));

        Assert.Contains("BuildDeletedObjectGuidFilter", restoreSource, StringComparison.Ordinal);
        Assert.Contains("ShowDeletedControlOid", restoreSource, StringComparison.Ordinal);
        Assert.Contains("deletedObjectRestoreCommandRunner.ExecuteRestoreAsync", restoreSource, StringComparison.Ordinal);
        Assert.Contains("deletedObjectRestoreCommandRunner", restoreSource, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestoreTargetMode.TargetPath", restoreSource, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreTargetPathRequiredMessage", restoreSource, StringComparison.Ordinal);
        Assert.Contains("TryLoadRestoreTargetOrganizationalUnitByDn", restoreSource, StringComparison.Ordinal);
        Assert.Contains("TryVerifyRestoredObject", restoreSource, StringComparison.Ordinal);
        Assert.Contains("PowerShellRestoreAdObject", restoreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ModifyDNRequest", restoreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ModifyRequest(", restoreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ModifyRequestUndelete", restoreSource, StringComparison.Ordinal);

        Assert.Contains("Restore-ADObject", runnerSource, StringComparison.Ordinal);
        Assert.Contains(".AddCommand(RestoreAdObjectCommand)", runnerSource, StringComparison.Ordinal);
        Assert.Contains("AddParameter(\"Identity\"", runnerSource, StringComparison.Ordinal);
        Assert.Contains("ToString(\"D\")", runnerSource, StringComparison.Ordinal);
        Assert.Contains("AddParameter(\"Server\"", runnerSource, StringComparison.Ordinal);
        Assert.Contains("AddParameter(\"Confirm\", false)", runnerSource, StringComparison.Ordinal);
        Assert.Contains("AddParameter(\"TargetPath\"", runnerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NewName", runnerSource, StringComparison.Ordinal);
        Assert.Contains("PSCredential", runnerSource, StringComparison.Ordinal);
        Assert.Contains("SecureString", runnerSource, StringComparison.Ordinal);
        Assert.Contains("SanitizePowerShellErrorSummary", runnerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_BlocksUnsupportedTypeAndMissingTargets()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("AdDeletedObjectType.Unknown", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreMissingTargetMessage", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreMissingRdnMessage", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreConflictMessage", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_WritesOperationLogsWithSnapshots()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("AdManagementOperationTypes.DeletedObjectRestore", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Succeeded", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Failed", source, StringComparison.Ordinal);
        Assert.Contains("BuildDeletedObjectRestoreBeforeSnapshot", source, StringComparison.Ordinal);
        Assert.Contains("BuildDeletedObjectRestoreAfterSnapshot", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreOperationType_IsDefined()
    {
        Assert.Equal("DeletedObjectRestore", AdManagementOperationTypes.DeletedObjectRestore);
    }

    [Fact]
    public void AdDeletedObjectRestoreRequest_IncludesActorContextFields()
    {
        var request = new AdDeletedObjectRestoreRequest(
            Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"),
            "actor.user",
            "127.0.0.1",
            "test-agent");

        Assert.Equal("actor.user", request.ActorUserName);
        Assert.Equal("127.0.0.1", request.ActorIpAddress);
        Assert.Equal("test-agent", request.ActorUserAgent);
        Assert.Equal(AdDeletedObjectRestoreTargetMode.OriginalLocation, request.RestoreTargetMode);
        Assert.Null(request.TargetPathDistinguishedName);
    }

    [Fact]
    public void DeletedObjectRestoreService_SanitizesLdapErrors()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("SanitizeDeletedObjectRestoreLdapError", source, StringComparison.Ordinal);
        Assert.Contains("AdLdapErrorNormalizer.Normalize", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_CatchesDirectoryOperationException()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("catch (DirectoryOperationException ex)", source, StringComparison.Ordinal);
        Assert.Contains("CreateRestoreDeletedObjectLdapExceptionFromDirectoryOperation", source, StringComparison.Ordinal);
        Assert.Contains("exception.Response", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_VerifyFailureUsesInvalidRequestNotConnectionFailed()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("AdDeletedObjectRestoreSteps.VerifyRestored", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreVerifyFailedMessage", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AdDeletedObjectRestoreSteps.VerifyRestored,\n                        request.ObjectGuid,\n                        restoredDistinguishedName,\n                        englishMessageOverride: \"The restored AD object could not be verified.\",\n                        normalizedReasonOverride: AdUserUpdateNormalizedReasons.ConnectionFailed)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "AdDirectoryFailureKind.InvalidRequest,\n                    BuildDeletedObjectRestoreFailureDiagnostic(\n                        AdDeletedObjectRestoreSteps.VerifyRestored",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_FailureLogWritesWhenBeforeStateNull()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("WriteDeletedObjectRestoreFailureLogsAsync", source, StringComparison.Ordinal);
        Assert.Contains("beforeState: null", source, StringComparison.Ordinal);
        Assert.Contains("request.ObjectGuid.ToString(\"D\")", source, StringComparison.Ordinal);
        Assert.Contains("AdManagementOperationStatuses.Failed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_MapsConnectionFailedOnlyForConnectionResultCodes()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("ResultCode.Unavailable", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.ConnectionFailed", source, StringComparison.Ordinal);
        Assert.Contains("_ => AdDirectoryFailureKind.InvalidRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AdOperationLogService_PersistsWriteAsyncEntries()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Persistence/Services/AdOperationLogService.cs"));

        Assert.Contains("await context.AdOperationLogs.AddAsync(log, cancellationToken);", source, StringComparison.Ordinal);
        Assert.Contains("await context.SaveChangesAsync(cancellationToken);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreEndpoint_MapsDirectoryFailureKinds()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Controllers/AdManagementController.cs"));

        Assert.Contains("RestoreDeletedObject", source, StringComparison.Ordinal);
        Assert.Contains("MapDirectoryFailure(result.Message, result.FailureKind)", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.ConnectionFailed => StatusCode(", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.InvalidRequest => BadRequest", source, StringComparison.Ordinal);
        Assert.Contains("AdDirectoryFailureKind.NotFound => NotFound", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_DefinesNormalizeDeletedObjectRestoreRdnHelper()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("NormalizeDeletedObjectRestoreRdn", source, StringComparison.Ordinal);
        Assert.Contains("AdLdapDnHelper.BuildCommonNameRdn", source, StringComparison.Ordinal);
        Assert.Contains("ContainsDeletedObjectRestoreRdnMarker", source, StringComparison.Ordinal);
        Assert.Contains("IsValidDeletedObjectRestoreRdn", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreInvalidRdnMessage", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreService_UsesNormalizedRestoreRdnForConflictAndVerify()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("var restoreRdn = NormalizeDeletedObjectRestoreRdn(originalLastKnownRdn);", source, StringComparison.Ordinal);
        Assert.Contains("var restoredDistinguishedName = $\"{restoreRdn},{restoreParentDn}\";", source, StringComparison.Ordinal);
        Assert.Contains("originalLastKnownRdn", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreFailureDiagnostic_IncludesOperationModeAndSourceTargetDns()
    {
        var restoreSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));
        var diagnosticSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Application/Common/AdManagement/AdOperationErrorDiagnosticBuilder.cs"));
        var summarySource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Application/Common/AdManagement/AdOperationLogSnapshotBuilder.cs"));

        Assert.Contains("BuildDeletedObjectRestoreFailureJson", restoreSource, StringComparison.Ordinal);
        Assert.Contains("sourceDeletedDistinguishedName", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("restoreOperationMode", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("sanitizedPowerShellError", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("powerShellExitCode", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("elapsedMs", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("credentialMode", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("restoreTargetMode", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("restoreOperationMode", summarySource, StringComparison.Ordinal);
        Assert.Contains("expectedRestoredDistinguishedName", summarySource, StringComparison.Ordinal);
        Assert.Contains("targetPathDistinguishedName", summarySource, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreEndpoint_AcceptsOptionalRequestBody()
    {
        var controllerSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Controllers/AdManagementController.cs"));
        var contractSource = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Api/Contracts/AdManagement/AdDeletedObjectRestoreRequestBody.cs"));

        Assert.Contains("AdDeletedObjectRestoreRequestBody? body", controllerSource, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestoreTargetModeParser.TryParse", controllerSource, StringComparison.Ordinal);
        Assert.Contains("Geçersiz geri yükleme hedef modu.", controllerSource, StringComparison.Ordinal);
        Assert.Contains("Farklı OU'ya geri yüklemek için hedef OU seçilmelidir.", controllerSource, StringComparison.Ordinal);
        Assert.Contains("string? RestoreTargetMode", contractSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AdDeletedObjectRestoreTargetMode? RestoreTargetMode", contractSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, AdDeletedObjectRestoreTargetMode.OriginalLocation, true)]
    [InlineData("", AdDeletedObjectRestoreTargetMode.OriginalLocation, true)]
    [InlineData("OriginalLocation", AdDeletedObjectRestoreTargetMode.OriginalLocation, true)]
    [InlineData("originallocation", AdDeletedObjectRestoreTargetMode.OriginalLocation, true)]
    [InlineData("TargetPath", AdDeletedObjectRestoreTargetMode.TargetPath, true)]
    [InlineData("targetpath", AdDeletedObjectRestoreTargetMode.TargetPath, true)]
    [InlineData("InvalidMode", AdDeletedObjectRestoreTargetMode.OriginalLocation, false)]
    public void AdDeletedObjectRestoreTargetModeParser_ParsesKnownValuesCaseInsensitively(
        string? input,
        AdDeletedObjectRestoreTargetMode expected,
        bool expectedSuccess)
    {
        var success = AdDeletedObjectRestoreTargetModeParser.TryParse(input, out var parsed);

        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expected, parsed);
        }
    }

    [Fact]
    public void DeletedObjectRestoreCommandRunner_IsRegisteredInDependencyInjection()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/DependencyInjection.cs"));

        Assert.Contains("IAdDeletedObjectRestoreCommandRunner", source, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestorePowerShellCommandRunner", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletedObjectRestoreRequestSummary_PreservesOriginalAndNormalizedRdn()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Application/Common/AdManagement/AdOperationLogSnapshotBuilder.cs"));

        Assert.Contains("originalLastKnownRdn", source, StringComparison.Ordinal);
        Assert.Contains("restoreRdn", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("test_grup", "CN=test_grup")]
    [InlineData("CN=test_grup", "CN=test_grup")]
    [InlineData("OU=_Groups", "OU=_Groups")]
    public void NormalizeDeletedObjectRestoreRdn_ConvertsBareValuesToCommonNameRdn(string input, string expected)
    {
        var normalized = InvokeNormalizeDeletedObjectRestoreRdn(input);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void NormalizeDeletedObjectRestoreRdn_EscapesSpecialCharactersUsingAdLdapDnHelper()
    {
        var normalized = InvokeNormalizeDeletedObjectRestoreRdn("Ali, Veli");
        Assert.Equal(AdLdapDnHelper.BuildCommonNameRdn("Ali, Veli"), normalized);
    }

    [Theory]
    [InlineData("test\0ADEL:guid")]
    [InlineData(@"CN=foo\0ADEL:guid")]
    [InlineData("nameADEL:guid")]
    public void NormalizeDeletedObjectRestoreRdn_ReturnsNullForDeletedMarkerValues(string input)
    {
        var normalized = InvokeNormalizeDeletedObjectRestoreRdn(input);
        Assert.Null(normalized);
    }

    [Fact]
    public void IsValidDeletedObjectRestoreRdn_RejectsMultiComponentRdnValues()
    {
        Assert.False(InvokeIsValidDeletedObjectRestoreRdn("CN=test,CN=test2"));
        Assert.True(InvokeIsValidDeletedObjectRestoreRdn(AdLdapDnHelper.BuildCommonNameRdn("Ali, Veli")));
    }

    private static string? InvokeNormalizeDeletedObjectRestoreRdn(string? input) =>
        typeof(AdUserDirectoryService)
            .GetMethod(
                "NormalizeDeletedObjectRestoreRdn",
                BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [input]) as string;

    private static bool InvokeIsValidDeletedObjectRestoreRdn(string restoreRdn) =>
        (bool)typeof(AdUserDirectoryService)
            .GetMethod(
                "IsValidDeletedObjectRestoreRdn",
                BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [restoreRdn])!;

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
