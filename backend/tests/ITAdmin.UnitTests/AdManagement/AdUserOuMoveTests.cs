using System.Reflection;
using System.Text.Json;
using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Persistence.Services;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdUserOuMoveTests
{
    [Fact]
    public void UsersMoveOuPermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Users.MoveOu", AdManagementPermissions.UsersMoveOu);
    }

    [Fact]
    public void DefaultPermissionSeed_ContainsUsersMoveOu()
    {
        var permissions = typeof(SetupService)
            .GetField("DefaultPermissions", BindingFlags.Static | BindingFlags.NonPublic)!
            .GetValue(null) as Array;

        Assert.NotNull(permissions);
        var containsMoveOu = permissions.Cast<object>().Any(item =>
        {
            var tuple = (ValueTuple<string, string, string>)item!;
            return string.Equals(tuple.Item2, AdManagementPermissions.UsersMoveOu, StringComparison.Ordinal);
        });

        Assert.True(containsMoveOu);
    }

    [Theory]
    [InlineData("OU=Child,OU=Users,DC=corp,DC=local", "OU=Users,DC=corp,DC=local", true)]
    [InlineData("OU=Users,DC=corp,DC=local", "OU=Users,DC=corp,DC=local", true)]
    [InlineData("OU=Groups,DC=corp,DC=local", "OU=Users,DC=corp,DC=local", false)]
    [InlineData("OU=Computers,OU=Servers,DC=corp,DC=local", "OU=Users,DC=corp,DC=local", false)]
    public void TargetOu_MustBeUsersRootOuOrDescendant(
        string targetOu,
        string usersRootOu,
        bool expectedAllowed)
    {
        var isAllowed = AdLdapDnHelper.IsEqualOrDescendantOf(targetOu, usersRootOu);
        Assert.Equal(expectedAllowed, isAllowed);
    }

    [Fact]
    public void SameParentOu_IsDetectedWithoutAdModify()
    {
        var userDn = "CN=Ali\\, Veli,OU=Source,OU=Users,DC=corp,DC=local";
        var targetOu = "OU=Source,OU=Users,DC=corp,DC=local";

        var parentOu = AdLdapDnHelper.GetParentDistinguishedName(userDn);

        Assert.True(AdLdapDnHelper.AreDistinguishedNamesEqual(parentOu, targetOu));
    }

    [Fact]
    public void GetParentDistinguishedName_HandlesEscapedCommaInCn()
    {
        var userDn = "CN=Ali\\, Veli,OU=Source,OU=Users,DC=corp,DC=local";

        var parent = AdLdapDnHelper.GetParentDistinguishedName(userDn);

        Assert.Equal("OU=Source,OU=Users,DC=corp,DC=local", parent);
    }

    [Fact]
    public void UserOuMove_RequestSummary_ContainsOperationUserAndTargetOu()
    {
        var userId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var targetOu = "OU=Target,OU=Users,DC=corp,DC=local";

        var json = AdOperationLogSnapshotBuilder.BuildUserOuMoveRequestSummary(userId, targetOu);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(AdManagementOperationTypes.UserOuMove, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal(userId.ToString("D"), document.RootElement.GetProperty("userId").GetString());
        Assert.Equal(targetOu, document.RootElement.GetProperty("targetOuDistinguishedName").GetString());
    }

    [Fact]
    public void UserOuMove_SuccessSnapshots_ContainUserAndOu()
    {
        var userId = "550e8400-e29b-41d4-a716-446655440000";
        const string sam = "mete.test2";
        const string upn = "mete.test2@corp.local";
        const string beforeDn = "CN=mete.test2,OU=Source,OU=Users,DC=corp,DC=local";
        const string afterDn = "CN=mete.test2,OU=Target,OU=Users,DC=corp,DC=local";
        const string beforeOu = "OU=Source,OU=Users,DC=corp,DC=local";
        const string afterOu = "OU=Target,OU=Users,DC=corp,DC=local";

        var beforeJson = AdOperationLogSnapshotBuilder.BuildUserOuMoveBeforeSnapshot(
            userId,
            sam,
            upn,
            beforeDn,
            beforeOu);
        var afterJson = AdOperationLogSnapshotBuilder.BuildUserOuMoveAfterSnapshot(
            userId,
            sam,
            upn,
            afterDn,
            afterOu);

        using var beforeDocument = JsonDocument.Parse(beforeJson);
        using var afterDocument = JsonDocument.Parse(afterJson);

        Assert.Equal(beforeDn, beforeDocument.RootElement.GetProperty("user").GetProperty("distinguishedName").GetString());
        Assert.Equal(beforeOu, beforeDocument.RootElement.GetProperty("ou").GetProperty("distinguishedName").GetString());
        Assert.Equal(afterDn, afterDocument.RootElement.GetProperty("user").GetProperty("distinguishedName").GetString());
        Assert.Equal(afterOu, afterDocument.RootElement.GetProperty("ou").GetProperty("distinguishedName").GetString());
    }

    [Fact]
    public void UserOuMove_FailureDiagnostic_UsesAdUserOuMoveFailedCode()
    {
        var userId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var diagnosticJson = AdOperationErrorDiagnosticBuilder.BuildUserOuMoveFailureJson(
            "MoveUser",
            userId,
            "CN=user,OU=Source,DC=corp,DC=local",
            normalizedReasonOverride: AdUserUpdateNormalizedReasons.NoSuchObject);

        var extractedCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson);

        Assert.Equal(AdOperationDiagnosticCodes.UserOuMoveFailed, extractedCode);
        using var document = JsonDocument.Parse(diagnosticJson);
        Assert.Equal(AdOperationDiagnosticCodes.UserOuMoveFailed, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(AdManagementOperationTypes.UserOuMove, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal("MoveUser", document.RootElement.GetProperty("step").GetString());
        Assert.False(document.RootElement.GetProperty("partialUpdate").GetBoolean());
        Assert.Equal("NotRequired", document.RootElement.GetProperty("rollbackStatus").GetString());
    }

    [Fact]
    public void ResolveDefaultCode_UserOuMove_ReturnsAdUserOuMoveFailed()
    {
        Assert.Equal(
            AdOperationDiagnosticCodes.UserOuMoveFailed,
            AdOperationErrorDiagnosticBuilder.ResolveDefaultCode(AdManagementOperationTypes.UserOuMove));
    }
}
