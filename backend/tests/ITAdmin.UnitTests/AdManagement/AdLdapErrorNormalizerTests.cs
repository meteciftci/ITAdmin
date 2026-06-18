using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdLdapErrorNormalizerTests
{
    [Theory]
    [InlineData("0000207D: NameErr: DSID-031A1234, problem 2006 (BAD_NAME), data 0, best match of: '' ")]
    [InlineData("name reference is invalid")]
    public void NormalizeMessageKey_MapsInvalidDnDiagnostic(string diagnostic)
    {
        var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(0, diagnostic);
        Assert.Equal(AdManagementApiMessageKeys.Ldap.InvalidDnSyntax, messageKey);
    }

    [Theory]
    [InlineData("0000052D: A constraint violation occurred.")]
    public void NormalizeMessageKey_MapsPasswordPolicyDiagnostic(string diagnostic)
    {
        var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(0, diagnostic);
        Assert.Equal(AdManagementApiMessageKeys.Ldap.ConstraintViolation, messageKey);
    }

    [Theory]
    [InlineData("00002098: Insufficient access rights")]
    [InlineData("00002089: Insufficient access rights")]
    public void NormalizeMessageKey_MapsInsufficientAccessDiagnostic(string diagnostic)
    {
        var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(0, diagnostic);
        Assert.Equal(AdManagementApiMessageKeys.Ldap.InsufficientAccessRights, messageKey);
    }

    [Theory]
    [InlineData("0000208F: ENTRY_EXISTS")]
    [InlineData("object already exists")]
    public void NormalizeMessageKey_MapsDuplicateEntryDiagnostic(string diagnostic)
    {
        var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(0, diagnostic);
        Assert.Equal(AdManagementApiMessageKeys.Ldap.EntryAlreadyExists, messageKey);
    }

    [Fact]
    public void NormalizeMessageKey_ReturnsUpdateUserFailedKey_WhenUnknown()
    {
        var messageKey = AdLdapErrorNormalizer.NormalizeMessageKey(9999, "totally unknown ldap failure");
        Assert.Equal(AdManagementApiMessageKeys.Ldap.UpdateUserFailed, messageKey);
    }
}
