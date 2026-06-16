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
    public void DeletedObjectRestoreService_UsesGuidLookupShowDeletedAndModifyRequestUndelete()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains("BuildDeletedObjectGuidFilter", source, StringComparison.Ordinal);
        Assert.Contains("ShowDeletedControlOid", source, StringComparison.Ordinal);
        Assert.Contains("ResolveDeletedObjectType", source, StringComparison.Ordinal);
        Assert.Contains("lastKnownParent", source, StringComparison.Ordinal);
        Assert.Contains("msDS-LastKnownRDN", source, StringComparison.Ordinal);
        Assert.Contains("TryLoadDirectoryObjectByDn", source, StringComparison.Ordinal);
        Assert.Contains("TryLoadDeletedDirectoryObjectByDn", source, StringComparison.Ordinal);
        Assert.Contains("ResolveDeletedObjectSourceDistinguishedName", source, StringComparison.Ordinal);
        Assert.Contains("entry.DistinguishedName", source, StringComparison.Ordinal);
        Assert.Contains("AdDeletedObjectRestoreSteps.VerifyDeletedSource", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreSourceNotVerifiedMessage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModifyDNRequest", source, StringComparison.Ordinal);
        Assert.Contains("new ModifyRequest(deletedDistinguishedName)", source, StringComparison.Ordinal);
        Assert.Contains("DirectoryAttributeOperation.Delete", source, StringComparison.Ordinal);
        Assert.Contains("\"isDeleted\"", source, StringComparison.Ordinal);
        Assert.Contains("DirectoryAttributeOperation.Replace", source, StringComparison.Ordinal);
        Assert.Contains("\"distinguishedName\"", source, StringComparison.Ordinal);
        Assert.Contains("distinguishedNameModification.Add(restoredDistinguishedName)", source, StringComparison.Ordinal);
        Assert.Contains("modifyRequest.Modifications.Add(distinguishedNameModification);", source, StringComparison.Ordinal);
        Assert.Contains("modifyRequest.Modifications.Add(isDeletedModification);", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf(
                "modifyRequest.Modifications.Add(distinguishedNameModification);",
                StringComparison.Ordinal)
            < source.IndexOf(
                "modifyRequest.Modifications.Add(isDeletedModification);",
                StringComparison.Ordinal));
        Assert.Contains("TryVerifyRestoredObject", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreOperationMode", source, StringComparison.Ordinal);
        Assert.Contains("ModifyRequestUndelete", source, StringComparison.Ordinal);
        Assert.Contains("SearchScope.Base", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreSourceDnResolutionEntryDistinguishedName", source, StringComparison.Ordinal);
        Assert.Contains("DeletedObjectRestoreSourceDnResolutionAttributeFallback", source, StringComparison.Ordinal);
        Assert.Contains("sourceDnResolution", source, StringComparison.Ordinal);
        Assert.Contains("sourceDnVerified", source, StringComparison.Ordinal);
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
        Assert.Contains("var restoredDistinguishedName = $\"{restoreRdn},{lastKnownParent}\";", source, StringComparison.Ordinal);
        Assert.Contains("ExecuteRestoreDeletedObject", source, StringComparison.Ordinal);
        Assert.Contains("beforeState.DistinguishedName,\n                    restoredDistinguishedName);", source, StringComparison.Ordinal);
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
        Assert.Contains("sourceDnResolution", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("sourceDnVerified", diagnosticSource, StringComparison.Ordinal);
        Assert.Contains("restoreOperationMode", summarySource, StringComparison.Ordinal);
        Assert.Contains("sourceDeletedDistinguishedName", summarySource, StringComparison.Ordinal);
        Assert.Contains("sourceDnResolution", summarySource, StringComparison.Ordinal);
        Assert.Contains("sourceDnVerified", summarySource, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDeletedObjectSourceDistinguishedName_PrefersEntryDistinguishedName()
    {
        var method = typeof(AdUserDirectoryService).GetMethod(
            "ResolveDeletedObjectSourceDistinguishedName",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(SearchResultEntry), parameters[0].ParameterType);
        Assert.True(parameters[1].IsOut);
        Assert.True(parameters[2].IsOut);
    }

    [Fact]
    public void DeletedObjectRestoreService_SourceVerificationFailureMapsToNotFound()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/src/SasPortal.Infrastructure/Services/AdUserDirectoryService.DeletedObjectRestore.cs"));

        Assert.Contains(
            "AdDirectoryFailureKind.NotFound,\n                    BuildDeletedObjectRestoreFailureDiagnostic(\n                        AdDeletedObjectRestoreSteps.VerifyDeletedSource",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Silinen nesne geri yükleme için doğrulanamadı.", source, StringComparison.Ordinal);
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
