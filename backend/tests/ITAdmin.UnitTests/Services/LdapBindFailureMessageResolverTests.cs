using System.DirectoryServices.Protocols;
using ITAdmin.Infrastructure.Services;

namespace ITAdmin.UnitTests.Services;

public sealed class LdapBindFailureMessageResolverTests
{
    [Theory]
    [InlineData(48)]
    [InlineData(49)]
    public void ResolveForServiceAccountBind_AuthenticationCodes_ReturnsServiceAccountMessage(int errorCode)
    {
        var ex = new LdapException(errorCode, "test");
        var message = LdapBindFailureMessageResolver.ResolveForServiceAccountBind(ex);
        Assert.Equal(LdapBindFailureMessageResolver.ServiceAccountBindFailedMessage, message);
    }

    [Theory]
    [InlineData(81)]
    [InlineData(85)]
    [InlineData(91)]
    [InlineData(51)]
    [InlineData(52)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(7)]
    public void ResolveForServiceAccountBind_ConnectionStyleCodes_ReturnsServerConnectionMessage(int errorCode)
    {
        var ex = new LdapException(errorCode, "test");
        var message = LdapBindFailureMessageResolver.ResolveForServiceAccountBind(ex);
        Assert.Equal(LdapBindFailureMessageResolver.ServerConnectionFailedMessage, message);
    }

    [Fact]
    public void ResolveForServiceAccountBind_UnknownCode_ReturnsValidationFailedMessage()
    {
        var ex = new LdapException(99, "test");
        var message = LdapBindFailureMessageResolver.ResolveForServiceAccountBind(ex);
        Assert.Equal(LdapBindFailureMessageResolver.ValidationFailedMessage, message);
    }

    [Theory]
    [InlineData(48, true)]
    [InlineData(49, true)]
    [InlineData(81, false)]
    public void IsCredentialFailure_ClassifiesOnlyAuthenticationCodes(int errorCode, bool expected)
    {
        Assert.Equal(expected, LdapBindFailureMessageResolver.IsCredentialFailure(new LdapException(errorCode)));
    }
}
