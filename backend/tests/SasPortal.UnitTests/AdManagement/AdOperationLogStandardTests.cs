using System.Text.Json;
using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdOperationLogStandardTests
{
    private static readonly Guid UserId = Guid.Parse("81e3c58c-99bc-4454-9edd-cfe4abb894b4");
    private const string GroupDn = "CN=ssl_PAMServer,OU=VPN,OU=_Groups,DC=muglabb,DC=lcl";

    [Fact]
    public void UserGroupAdd_RequestSummary_ContainsOperationUserAndGroupIntent()
    {
        var json = AdOperationLogSnapshotBuilder.BuildGroupMembershipRequestSummary(
            AdManagementOperationTypes.UserGroupAdd,
            UserId,
            GroupDn,
            "ssl_PAMServer");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdManagementOperationTypes.UserGroupAdd, root.GetProperty("operation").GetString());
        Assert.Equal(UserId.ToString("D"), root.GetProperty("userId").GetString());
        Assert.Equal(GroupDn, root.GetProperty("groupDistinguishedName").GetString());
        Assert.Equal("ssl_PAMServer", root.GetProperty("groupName").GetString());
    }

    [Fact]
    public void UserGroupAdd_BeforeSnapshot_ContainsDirectMemberFalse()
    {
        var json = AdOperationLogSnapshotBuilder.BuildGroupMembershipBeforeSnapshot(
            AdManagementOperationTypes.UserGroupAdd,
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            "ssl_PAMServer",
            GroupDn,
            isDirectMember: false);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void UserGroupAdd_AfterSnapshot_ContainsDirectMemberTrue()
    {
        var json = AdOperationLogSnapshotBuilder.BuildGroupMembershipAfterSnapshot(
            AdManagementOperationTypes.UserGroupAdd,
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            "ssl_PAMServer",
            GroupDn,
            isDirectMember: true);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void UserGroupRemove_BeforeSnapshot_ContainsDirectMemberTrue()
    {
        var json = AdOperationLogSnapshotBuilder.BuildGroupMembershipBeforeSnapshot(
            AdManagementOperationTypes.UserGroupRemove,
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            "ssl_PAMServer",
            GroupDn,
            isDirectMember: true);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void UserGroupRemove_AfterSnapshot_ContainsDirectMemberFalse()
    {
        var json = AdOperationLogSnapshotBuilder.BuildGroupMembershipAfterSnapshot(
            AdManagementOperationTypes.UserGroupRemove,
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            "ssl_PAMServer",
            GroupDn,
            isDirectMember: false);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void GroupOperationFailureDiagnostic_ContainsMatchingErrorCode()
    {
        var json = AdOperationErrorDiagnosticBuilder.BuildGroupMembershipFailureJson(
            AdManagementOperationTypes.UserGroupAdd,
            "ModifyGroupMembership",
            UserId,
            "CN=Mete,DC=example,DC=com",
            ldapResultCode: 50,
            ldapExceptionErrorCode: 50,
            ldapDiagnosticMessage: "Insufficient access rights");

        var extractedCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(json);

        Assert.Equal(AdOperationDiagnosticCodes.UserGroupAddFailed, extractedCode);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(AdOperationDiagnosticCodes.UserGroupAddFailed, document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void UserCreate_RequestSummary_DoesNotContainPassword()
    {
        var request = new CreateAdUserRequest(
            "Mete",
            "Test",
            null,
            "mete.test",
            "mugla.bel.tr",
            "OU=_Users,DC=muglabb,DC=lcl",
            "SuperSecretPassword123!",
            true,
            true,
            [new CreateAdUserMappedAttributeRequest("gender", "Male")],
            null,
            null,
            null,
            null);

        var json = AdOperationLogSnapshotBuilder.BuildCreateRequestSummary(request);

        Assert.DoesNotContain("SuperSecretPassword123!", json, StringComparison.Ordinal);
        Assert.DoesNotContain("initialPassword", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"password\"", json, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        Assert.Contains(
            document.RootElement.GetProperty("mappedAttributeFields").EnumerateArray(),
            element => string.Equals("gender", element.GetString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UserCreate_AfterSnapshot_ContainsCreatedUserStateAndMasksSensitiveValues()
    {
        var response = new CreateAdUserResponse(
            UserId.ToString("D"),
            "CN=Mete Test,OU=_Users,DC=muglabb,DC=lcl",
            "Mete Test",
            "mete.test",
            "mete.test@mugla.bel.tr",
            "Mete Test",
            true,
            "created",
            false,
            null);

        var mappings = new[]
        {
            new AdAttributeMappingItem(
                Guid.NewGuid(),
                "nationalId",
                "National ID",
                "extensionAttribute1",
                IsEnabled: true,
                IsEditable: true,
                IsSensitive: true,
                IsSearchable: false,
                ValidationType: "None",
                MaskingStrategy: "Hidden",
                SortOrder: 1),
        };

        var json = AdOperationLogSnapshotBuilder.BuildCreateAfterSnapshot(
            response,
            isEnabled: true,
            [new CreateAdUserMappedAttributeRequest("nationalId", "12345678901")],
            mappings);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("mete.test", document.RootElement.GetProperty("user").GetProperty("samAccountName").GetString());
        Assert.True(document.RootElement.GetProperty("account").GetProperty("isEnabled").GetBoolean());
        Assert.Equal(
            "••••",
            document.RootElement.GetProperty("mappedAttributes")[0].GetProperty("values")[0].GetString());
        Assert.DoesNotContain("12345678901", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UserEnable_BeforeAndAfterSnapshots_ContainEnabledState()
    {
        var before = AdOperationLogSnapshotBuilder.BuildAccountBeforeSnapshot(
            AdManagementOperationTypes.UserEnable,
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            isEnabled: false,
            isLockedOut: false,
            userAccountControl: 514,
            lockoutTime: null);

        var after = AdOperationLogSnapshotBuilder.BuildAccountAfterSnapshot(
            AdManagementOperationTypes.UserEnable,
            isEnabled: true,
            isLockedOut: false,
            userAccountControl: 512,
            lockoutTime: null);

        using var beforeDocument = JsonDocument.Parse(before);
        using var afterDocument = JsonDocument.Parse(after);

        Assert.False(beforeDocument.RootElement.GetProperty("account").GetProperty("isEnabled").GetBoolean());
        Assert.True(afterDocument.RootElement.GetProperty("account").GetProperty("isEnabled").GetBoolean());
    }

    [Fact]
    public void UserUnlock_BeforeAndAfterSnapshots_ContainLockedState()
    {
        var before = AdOperationLogSnapshotBuilder.BuildAccountBeforeSnapshot(
            AdManagementOperationTypes.UserUnlock,
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            isEnabled: true,
            isLockedOut: true,
            userAccountControl: 512,
            lockoutTime: 133_000_000_000_000);

        var after = AdOperationLogSnapshotBuilder.BuildAccountAfterSnapshot(
            AdManagementOperationTypes.UserUnlock,
            isEnabled: true,
            isLockedOut: false,
            userAccountControl: 512,
            lockoutTime: 0);

        using var beforeDocument = JsonDocument.Parse(before);
        using var afterDocument = JsonDocument.Parse(after);

        Assert.True(beforeDocument.RootElement.GetProperty("account").GetProperty("isLocked").GetBoolean());
        Assert.False(afterDocument.RootElement.GetProperty("account").GetProperty("isLocked").GetBoolean());
        Assert.Null(afterDocument.RootElement.GetProperty("account").GetProperty("lockoutTime").GetString());
    }

    [Fact]
    public void UserUpdateFailureDiagnostic_ExtractsErrorCode()
    {
        var json = AdUserUpdateOperationDiagnosticBuilder.BuildPreflightDuplicateJson(
            "sAMAccountName",
            "Duplicate samAccountName",
            UserId);

        var code = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(json);

        Assert.Equal(AdUserUpdateDiagnosticCodes.PreflightFailed, code);
    }
}
