using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdLdapErrorNormalizerTests
{
    [Theory]
    [InlineData("0000207D: NameErr: DSID-031A1234, problem 2006 (BAD_NAME), data 0, best match of: '' ")]
    [InlineData("name reference is invalid")]
    public void Normalize_MapsInvalidDnDiagnosticToUserFriendlyMessage(string diagnostic)
    {
        var message = AdLdapErrorNormalizer.Normalize(0, diagnostic);

        Assert.Equal(AdLdapErrorNormalizer.InvalidDnSyntaxMessage, message);
    }

    [Theory]
    [InlineData("0000052D: A constraint violation occurred.")]
    public void Normalize_MapsPasswordPolicyDiagnosticToConstraintMessage(string diagnostic)
    {
        var message = AdLdapErrorNormalizer.Normalize(0, diagnostic);

        Assert.Equal(AdLdapErrorNormalizer.ConstraintViolationMessage, message);
    }

    [Theory]
    [InlineData("00002098: Insufficient access rights")]
    [InlineData("00002089: Insufficient access rights")]
    public void Normalize_MapsInsufficientAccessDiagnostic(string diagnostic)
    {
        var message = AdLdapErrorNormalizer.Normalize(0, diagnostic);

        Assert.Equal(AdLdapErrorNormalizer.InsufficientAccessRightsMessage, message);
    }

    [Theory]
    [InlineData("0000208F: ENTRY_EXISTS")]
    [InlineData("object already exists")]
    public void Normalize_MapsDuplicateEntryDiagnostic(string diagnostic)
    {
        var message = AdLdapErrorNormalizer.Normalize(0, diagnostic);

        Assert.Equal(AdLdapErrorNormalizer.EntryAlreadyExistsMessage, message);
    }

    [Fact]
    public void Normalize_ReturnsGenericMessage_WhenUnknown()
    {
        var message = AdLdapErrorNormalizer.Normalize(9999, "totally unknown ldap failure");

        Assert.Equal(AdLdapErrorNormalizer.UpdateUserFailedMessage, message);
    }
}
