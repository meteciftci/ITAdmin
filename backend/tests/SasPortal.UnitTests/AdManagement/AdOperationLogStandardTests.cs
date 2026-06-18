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
    public void UserGroupRemove_BeforeSnapshot_ContainsOperationUserGroupAndMembership()
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
        var root = document.RootElement;

        Assert.Equal(AdManagementOperationTypes.UserGroupRemove, root.GetProperty("operation").GetString());
        Assert.Equal(UserId.ToString("D"), root.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal("ssl_PAMServer", root.GetProperty("group").GetProperty("name").GetString());
        Assert.True(root.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
    }

    [Fact]
    public void UserGroupRemove_AfterSnapshot_ContainsOperationUserGroupAndMembership()
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
        var root = document.RootElement;

        Assert.Equal(AdManagementOperationTypes.UserGroupRemove, root.GetProperty("operation").GetString());
        Assert.Equal(UserId.ToString("D"), root.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(GroupDn, root.GetProperty("group").GetProperty("distinguishedName").GetString());
        Assert.False(root.GetProperty("membership").GetProperty("isDirectMember").GetBoolean());
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
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            isEnabled: true,
            isLockedOut: false,
            userAccountControl: 512,
            lockoutTime: null);

        using var beforeDocument = JsonDocument.Parse(before);
        using var afterDocument = JsonDocument.Parse(after);

        Assert.False(beforeDocument.RootElement.GetProperty("account").GetProperty("isEnabled").GetBoolean());
        Assert.True(afterDocument.RootElement.GetProperty("account").GetProperty("isEnabled").GetBoolean());
        Assert.Equal(UserId.ToString("D"), afterDocument.RootElement.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(AdManagementOperationTypes.UserEnable, afterDocument.RootElement.GetProperty("operation").GetString());
    }

    [Fact]
    public void UserDisable_AfterSnapshot_ContainsOperationUserAndAccount()
    {
        var after = AdOperationLogSnapshotBuilder.BuildAccountAfterSnapshot(
            AdManagementOperationTypes.UserDisable,
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            isEnabled: false,
            isLockedOut: false,
            userAccountControl: 514,
            lockoutTime: null);

        using var document = JsonDocument.Parse(after);
        Assert.Equal(AdManagementOperationTypes.UserDisable, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal(UserId.ToString("D"), document.RootElement.GetProperty("user").GetProperty("id").GetString());
        Assert.False(document.RootElement.GetProperty("account").GetProperty("isEnabled").GetBoolean());
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
            UserId.ToString("D"),
            "mete.ciftci",
            "mete.ciftci@mugla.bel.tr",
            "CN=Mete,DC=example,DC=com",
            isEnabled: true,
            isLockedOut: false,
            userAccountControl: 512,
            lockoutTime: 0);

        using var beforeDocument = JsonDocument.Parse(before);
        using var afterDocument = JsonDocument.Parse(after);

        Assert.True(beforeDocument.RootElement.GetProperty("account").GetProperty("isLocked").GetBoolean());
        Assert.False(afterDocument.RootElement.GetProperty("account").GetProperty("isLocked").GetBoolean());
        Assert.False(afterDocument.RootElement.GetProperty("account").TryGetProperty("lockoutTime", out _));
        Assert.Equal(UserId.ToString("D"), afterDocument.RootElement.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(AdManagementOperationTypes.UserUnlock, afterDocument.RootElement.GetProperty("operation").GetString());
    }

    [Fact]
    public void SettingsValidatedFailureDiagnostic_ContainsMatchingErrorCodeAndJsonMessage()
    {
        var result = new AdManagementValidationResult(
            IsValid: false,
            MessageKey: "AD yönetim servis hesabı ile bağlantı kurulamadı.",
            CheckedAt: DateTimeOffset.UtcNow,
            Details:
            [
                new AdManagementValidationDetail(
                    "serviceAccountBind",
                    AdManagementValidationStatuses.Failed,
                    "AD yönetim servis hesabı ile bağlantı kurulamadı."),
            ]);

        var summaryJson = AdOperationLogSnapshotBuilder.BuildSettingsValidationSummary(result);
        var diagnosticJson = AdOperationErrorDiagnosticBuilder.BuildSettingsValidationFailureJson(result);
        var errorCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson);

        Assert.Equal(AdOperationDiagnosticCodes.SettingsValidationFailed, errorCode);
        using var summaryDocument = JsonDocument.Parse(summaryJson);
        Assert.Equal(AdManagementOperationTypes.SettingsValidated, summaryDocument.RootElement.GetProperty("operation").GetString());

        using var diagnosticDocument = JsonDocument.Parse(diagnosticJson);
        Assert.Equal(AdOperationDiagnosticCodes.SettingsValidationFailed, diagnosticDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal("ValidateConnection", diagnosticDocument.RootElement.GetProperty("step").GetString());
    }

    [Fact]
    public void SettingsUpdatedSnapshots_DoNotContainPasswordFields()
    {
        var entity = new SasPortal.Domain.Entities.AdManagementSettings
        {
            IsEnabled = true,
            DomainFqdn = "corp.example.com",
            NetbiosDomainName = "CORP",
            DefaultNamingContext = "DC=corp,DC=example,DC=com",
            BaseDn = "DC=corp,DC=example,DC=com",
            UsersRootOu = "OU=Users,DC=corp,DC=example,DC=com",
            DisabledUsersOu = "OU=Disabled,DC=corp,DC=example,DC=com",
            ServiceAccountUserName = "svc_ad",
            EncryptedServiceAccountPassword = "protected:secret",
            PowerShellHealthEnabled = false,
            PowerShellTimeoutSeconds = 30,
            LastValidationStatus = "Ok",
        };

        var snapshotJson = AdOperationLogSnapshotBuilder.BuildSettingsSnapshot(
            entity,
            ["dc01.corp.example.com"],
            AdManagementNotificationSettingsSerializer.CreateDefault());

        Assert.DoesNotContain("protected:secret", snapshotJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"serviceAccountPassword\"", snapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("encryptedServiceAccountPassword", snapshotJson, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(snapshotJson);
        Assert.True(document.RootElement.GetProperty("hasServiceAccountPassword").GetBoolean());
        Assert.Equal("corp.example.com", document.RootElement.GetProperty("domainFqdn").GetString());
    }

    [Fact]
    public void AttributeMappingCreatedSnapshots_FollowStandard()
    {
        var request = new CreateAdAttributeMappingRequest(
            "mobilePhone",
            "Mobile Phone",
            "telephoneNumber",
            IsEnabled: true,
            IsEditable: true,
            IsSensitive: true,
            IsSearchable: false,
            ValidationType: "Phone",
            MaskingStrategy: "Phone",
            SortOrder: 1,
            ActorUserId: null,
            ActorUserName: null,
            ActorIpAddress: null,
            ActorUserAgent: null);

        var requestJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingCreateRequestSummary(request);
        using var requestDocument = JsonDocument.Parse(requestJson);
        Assert.Equal(AdManagementOperationTypes.AttributeMappingCreated, requestDocument.RootElement.GetProperty("operation").GetString());
        Assert.Equal("mobilePhone", requestDocument.RootElement.GetProperty("logicalField").GetString());
    }

    [Fact]
    public void AttributeMappingUpdatedSnapshots_IncludeChangedFields()
    {
        var before = new SasPortal.Domain.Entities.AdAttributeMapping
        {
            LogicalField = "mobilePhone",
            DisplayName = "Old Name",
            AttributeName = "telephoneNumber",
            IsEnabled = true,
            IsEditable = true,
            IsSensitive = false,
            IsSearchable = true,
            ValidationType = "None",
            MaskingStrategy = "None",
            SortOrder = 1,
        };

        var request = new UpdateAdAttributeMappingRequest(
            before.Id,
            "New Name",
            "mobile",
            IsEnabled: false,
            IsEditable: true,
            IsSensitive: true,
            IsSearchable: false,
            ValidationType: "Phone",
            MaskingStrategy: "Phone",
            SortOrder: 2,
            ActorUserId: null,
            ActorUserName: null,
            ActorIpAddress: null,
            ActorUserAgent: null);

        var requestJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingUpdateRequestSummary(request, before);
        using var document = JsonDocument.Parse(requestJson);
        var changedFields = document.RootElement.GetProperty("changedFields").EnumerateArray().Select(static x => x.GetString()).ToList();

        Assert.Contains("displayName", changedFields);
        Assert.Contains("attributeName", changedFields);
        Assert.Contains("isEnabled", changedFields);
        Assert.Contains("sortOrder", changedFields);
    }

    [Fact]
    public void AttributeMappingDeletedRequestSummary_ContainsOperationAndLogicalField()
    {
        var entity = new SasPortal.Domain.Entities.AdAttributeMapping
        {
            LogicalField = "nationalId",
        };

        var request = new DeleteAdAttributeMappingRequest(
            entity.Id,
            ActorUserId: null,
            ActorUserName: null,
            ActorIpAddress: null,
            ActorUserAgent: null);

        var requestJson = AdOperationLogSnapshotBuilder.BuildAttributeMappingDeleteRequestSummary(request, entity);
        using var document = JsonDocument.Parse(requestJson);
        Assert.Equal(AdManagementOperationTypes.AttributeMappingDeleted, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal("nationalId", document.RootElement.GetProperty("logicalField").GetString());
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

    [Fact]
    public void GroupCreate_AfterSnapshot_UsesNestedOperationAndGroupFormat()
    {
        var json = AdOperationLogSnapshotBuilder.BuildGroupCreateAfterSnapshot(CreateSampleGroupDetail());

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(AdManagementOperationTypes.GroupCreate, root.GetProperty("operation").GetString());
        Assert.True(root.TryGetProperty("group", out var group));
        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", group.GetProperty("id").GetString());
        Assert.Equal("VPN Users", group.GetProperty("displayName").GetString());
        Assert.Equal("vpn-users", group.GetProperty("name").GetString());
        Assert.Equal("VPN Users", group.GetProperty("cn").GetString());
        Assert.Equal("vpn-users", group.GetProperty("samAccountName").GetString());
        Assert.Equal("VPN access group", group.GetProperty("description").GetString());
        Assert.Equal("CN=VPN Users,OU=Groups,DC=corp,DC=local", group.GetProperty("distinguishedName").GetString());
        Assert.Equal("Global", group.GetProperty("groupScope").GetString());
        Assert.True(group.GetProperty("securityEnabled").GetBoolean());
        Assert.Equal(-2147483646, group.GetProperty("groupType").GetInt32());
    }

    [Fact]
    public void GroupUpdate_BeforeAndAfterSnapshots_UseNestedOperationAndGroupFormat()
    {
        var before = CreateSampleGroupDetail(
            name: "vpn-users-old",
            cn: "VPN Users Old",
            samAccountName: "vpn-users-old",
            distinguishedName: "CN=VPN Users Old,OU=Groups,DC=corp,DC=local",
            description: "Old description");

        var after = CreateSampleGroupDetail(
            name: "vpn-users",
            cn: "VPN Users",
            samAccountName: "vpn-users",
            distinguishedName: "CN=VPN Users,OU=Groups,DC=corp,DC=local",
            description: "Updated description");

        var beforeJson = AdGroupUpdateSnapshotBuilder.Build(before);
        var afterJson = AdGroupUpdateSnapshotBuilder.Build(after);

        using var beforeDocument = JsonDocument.Parse(beforeJson);
        using var afterDocument = JsonDocument.Parse(afterJson);

        Assert.Equal(AdManagementOperationTypes.GroupUpdate, beforeDocument.RootElement.GetProperty("operation").GetString());
        Assert.Equal(AdManagementOperationTypes.GroupUpdate, afterDocument.RootElement.GetProperty("operation").GetString());
        Assert.True(beforeDocument.RootElement.TryGetProperty("group", out var beforeGroup));
        Assert.True(afterDocument.RootElement.TryGetProperty("group", out var afterGroup));
        Assert.False(beforeDocument.RootElement.TryGetProperty("id", out _));
        Assert.Equal("vpn-users-old", beforeGroup.GetProperty("name").GetString());
        Assert.Equal("vpn-users", afterGroup.GetProperty("name").GetString());
        Assert.Equal("CN=VPN Users Old,OU=Groups,DC=corp,DC=local", beforeGroup.GetProperty("distinguishedName").GetString());
        Assert.Equal("CN=VPN Users,OU=Groups,DC=corp,DC=local", afterGroup.GetProperty("distinguishedName").GetString());
    }

    [Fact]
    public void GroupUpdate_RequestSummary_ContainsOperation()
    {
        var request = new UpdateAdGroupRequest(
            GroupId: Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            DisplayName: "VPN Users",
            Name: "vpn-users",
            SamAccountName: "vpn-users",
            Description: "Updated description",
            ActorUserId: null,
            ActorUserName: null,
            ActorIpAddress: null,
            ActorUserAgent: null);

        var json = AdGroupUpdateSnapshotBuilder.BuildRequestSummary(request);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(AdManagementOperationTypes.GroupUpdate, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal("vpn-users", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void GroupDelete_RequestSummary_ContainsOperationAndGroupIdentity()
    {
        var group = CreateSampleGroupDetail(memberCount: 12, memberOfCount: 3);
        var request = new DeleteAdGroupRequest(
            GroupId: Guid.Parse(group.Id),
            ActorUserId: null,
            ActorUserName: null,
            ActorIpAddress: null,
            ActorUserAgent: null);

        var json = AdGroupDeleteSnapshotBuilder.BuildRequestSummary(request, group);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(AdManagementOperationTypes.GroupDelete, document.RootElement.GetProperty("operation").GetString());
        Assert.Equal(group.Id, document.RootElement.GetProperty("groupId").GetString());
        Assert.Equal("vpn-users", document.RootElement.GetProperty("samAccountName").GetString());
        Assert.Equal("vpn-users", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(
            "CN=VPN Users,OU=Groups,DC=corp,DC=local",
            document.RootElement.GetProperty("distinguishedName").GetString());
    }

    [Fact]
    public void GroupDelete_BeforeSnapshot_ContainsNestedGroupWithMemberCounts()
    {
        var group = CreateSampleGroupDetail(memberCount: 12, memberOfCount: 3);
        var json = AdGroupDeleteSnapshotBuilder.Build(group);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(AdManagementOperationTypes.GroupDelete, document.RootElement.GetProperty("operation").GetString());
        Assert.True(document.RootElement.TryGetProperty("group", out var groupNode));
        Assert.Equal(12, groupNode.GetProperty("memberCount").GetInt32());
        Assert.Equal(3, groupNode.GetProperty("memberOfCount").GetInt32());
        Assert.Equal("vpn-users", groupNode.GetProperty("samAccountName").GetString());
    }

    [Fact]
    public void GroupDelete_FailureDiagnostic_ContainsExtractableErrorCode()
    {
        var diagnosticJson = AdGroupDeleteOperationDiagnosticBuilder.BuildGenericFailureJson(
            "DeleteGroup",
            AdUserUpdateNormalizedReasons.DeleteFailed,
            "The AD security group could not be deleted.",
            Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            "CN=VPN Users,OU=Groups,DC=corp,DC=local");

        var extractedCode = AdOperationLogErrorCodeExtractor.TryExtractFromDiagnosticJson(diagnosticJson);

        using var document = JsonDocument.Parse(diagnosticJson);
        Assert.Equal(AdOperationDiagnosticCodes.GroupDeleteFailed, extractedCode);
        Assert.Equal(AdOperationDiagnosticCodes.GroupDeleteFailed, document.RootElement.GetProperty("code").GetString());
        Assert.Equal("GroupDelete", document.RootElement.GetProperty("operation").GetString());
        Assert.Equal("DeleteGroup", document.RootElement.GetProperty("step").GetString());
    }

    private static AdGroupDetail CreateSampleGroupDetail(
        string id = "550e8400-e29b-41d4-a716-446655440000",
        string name = "vpn-users",
        string? cn = "VPN Users",
        string? samAccountName = "vpn-users",
        string distinguishedName = "CN=VPN Users,OU=Groups,DC=corp,DC=local",
        string? description = "VPN access group",
        int memberCount = 0,
        int memberOfCount = 0) =>
        new(
            Id: id,
            DistinguishedName: distinguishedName,
            DisplayName: "VPN Users",
            Name: name,
            Cn: cn,
            SamAccountName: samAccountName,
            Description: description,
            GroupScope: "Global",
            SecurityEnabled: true,
            GroupType: -2147483646,
            WhenCreated: null,
            WhenChanged: null,
            ManagedByDistinguishedName: null,
            ManagedByDisplayName: null,
            MemberCount: memberCount,
            MemberOfCount: memberOfCount,
            Members: Array.Empty<AdGroupMemberItem>(),
            MemberOf: Array.Empty<AdGroupMemberItem>(),
            MembersTruncated: false,
            MemberOfTruncated: false);
}
