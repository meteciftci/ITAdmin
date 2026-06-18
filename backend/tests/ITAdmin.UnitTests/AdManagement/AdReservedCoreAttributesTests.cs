using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdReservedCoreAttributesTests
{
    [Theory]
    [InlineData("mail")]
    [InlineData("MAIL")]
    [InlineData("sAMAccountName")]
    [InlineData("userPrincipalName")]
    [InlineData("department")]
    public void IsReserved_ReturnsTrue_ForCoreAttributes(string attributeName)
    {
        Assert.True(AdReservedCoreAttributes.IsReserved(attributeName));
    }

    [Theory]
    [InlineData("extensionAttribute1")]
    [InlineData("mobile")]
    [InlineData("employeeNumber")]
    public void IsReserved_ReturnsFalse_ForCustomAttributes(string attributeName)
    {
        Assert.False(AdReservedCoreAttributes.IsReserved(attributeName));
    }
}
