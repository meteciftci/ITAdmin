using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdUpdateUserInfrastructureTests
{
    [Fact]
    public void GetLdapsRequiredErrorMessage_WhenSslDisabled_ReturnsUserFriendlyMessage()
    {
        var message = AdDirectoryConnectionRequirements.GetLdapsRequiredErrorMessage(useSsl: false);

        Assert.Equal(AdDirectoryConnectionRequirements.LdapsRequiredMessage, message);
    }

    [Fact]
    public void GetLdapsRequiredErrorMessage_WhenSslEnabled_ReturnsNull()
    {
        Assert.Null(AdDirectoryConnectionRequirements.GetLdapsRequiredErrorMessage(useSsl: true));
    }

    [Fact]
    public void UsersUpdatePermissionConstant_IsDefined()
    {
        Assert.Equal("AdManagement.Users.Update", AdManagementPermissions.UsersUpdate);
    }

    [Fact]
    public void TryValidate_WhenSamAccountNameTooLong_ReturnsInvalidRequestMessage()
    {
        var mappings = Array.Empty<AdAttributeMappingItem>();
        var request = new UpdateAdUserRequest(
            Guid.NewGuid(),
            "Ali",
            "Veli",
            "Ali Veli",
            new string('a', 21),
            "ali.veli@corp.example.com",
            null,
            null,
            [],
            null,
            null,
            null,
            null);

        var isValid = AdUpdateUserRequestValidator.TryValidate(request, mappings, out var message);

        Assert.False(isValid);
        Assert.Equal(AdSamAccountNameValidator.TooLongMessage, message);
    }

    [Fact]
    public void DeriveCommonNameFromDisplayName_UsesTrimmedDisplayName()
    {
        var cn = AdUpdateUserRequestValidator.DeriveCommonNameFromDisplayName("  Mete Çiftçi  ");

        Assert.Equal("Mete Çiftçi", cn);
    }

    [Fact]
    public void TryValidate_WhenMappedFieldUsesReservedCoreAttribute_ReturnsError()
    {
        var mappings = new[]
        {
            new AdAttributeMappingItem(
                Guid.NewGuid(),
                "workMail",
                "Work Mail",
                "mail",
                IsEnabled: true,
                IsEditable: true,
                IsSensitive: false,
                IsSearchable: false,
                ValidationType: "Email",
                MaskingStrategy: "None",
                SortOrder: 1),
        };

        var request = new UpdateAdUserRequest(
            Guid.NewGuid(),
            "Ali",
            "Veli",
            "Ali Veli",
            "ali.veli",
            "ali.veli@corp.example.com",
            null,
            null,
            [new UpdateAdUserMappedAttributeRequest("workMail", "other@corp.example.com")],
            null,
            null,
            null,
            null);

        var isValid = AdUpdateUserRequestValidator.TryValidate(request, mappings, out var message);

        Assert.False(isValid);
        Assert.Equal(AdReservedCoreAttributes.ReservedAttributeMappingMessage, message);
    }

    [Fact]
    public void TryValidate_WhenMappedFieldNotEditable_ReturnsError()
    {
        var mappings = new[]
        {
            new AdAttributeMappingItem(
                Guid.NewGuid(),
                "mobilePhone",
                "Mobile",
                "mobile",
                IsEnabled: true,
                IsEditable: false,
                IsSensitive: true,
                IsSearchable: false,
                ValidationType: "Phone",
                MaskingStrategy: "Phone",
                SortOrder: 1),
        };

        var request = new UpdateAdUserRequest(
            Guid.NewGuid(),
            "Ali",
            "Veli",
            "Ali Veli",
            "ali.veli",
            "ali.veli@corp.example.com",
            null,
            null,
            [new UpdateAdUserMappedAttributeRequest("mobilePhone", "+905551112233")],
            null,
            null,
            null,
            null);

        var isValid = AdUpdateUserRequestValidator.TryValidate(request, mappings, out var message);

        Assert.False(isValid);
        Assert.Contains("mobilePhone", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMappedAttributesForSnapshot_MasksSensitiveValues()
    {
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

        var snapshotAttributes = AdUserUpdateSnapshotBuilder.BuildMappedAttributesForSnapshot(
            _ => ["12345678901"],
            mappings);

        Assert.Single(snapshotAttributes);
        Assert.Equal(["••••"], snapshotAttributes[0].Value);
    }

    [Theory]
    [InlineData(68, AdLdapErrorNormalizer.EntryAlreadyExistsMessage)]
    [InlineData(32, AdLdapErrorNormalizer.NoSuchObjectMessage)]
    [InlineData(50, AdLdapErrorNormalizer.InsufficientAccessRightsMessage)]
    public void Normalize_ReturnsExpectedUserMessage(int errorCode, string expected)
    {
        Assert.Equal(expected, AdLdapErrorNormalizer.Normalize(errorCode));
    }

    [Fact]
    public void GetParentDistinguishedName_ReturnsParentForUserDn()
    {
        const string userDn = "CN=Ali Veli,OU=Users,DC=corp,DC=example,DC=com";
        var parent = AdLdapDnHelper.GetParentDistinguishedName(userDn);

        Assert.Equal("OU=Users,DC=corp,DC=example,DC=com", parent);
    }

    [Fact]
    public void BuildCommonNameRdn_EscapesSpecialCharacters()
    {
        var rdn = AdLdapDnHelper.BuildCommonNameRdn("Ali, Veli");

        Assert.StartsWith("CN=", rdn, StringComparison.Ordinal);
        Assert.Contains("\\,", rdn, StringComparison.Ordinal);
    }
}
