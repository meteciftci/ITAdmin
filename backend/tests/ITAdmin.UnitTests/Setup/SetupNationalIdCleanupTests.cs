using System.Reflection;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.UnitTests.Setup;

public sealed class SetupNationalIdCleanupTests
{
    [Fact]
    public void SetupAndLdapModels_DoNotContainNationalIdAttributeProperty()
    {
        var modelTypes = new[]
        {
            typeof(LdapUserProfileRequest),
            typeof(LdapUserProfileByObjectIdRequest),
            typeof(LdapUserLookupRequest),
            typeof(CompleteSetupLdapSettings),
        };

        foreach (var modelType in modelTypes)
        {
            Assert.DoesNotContain(
                modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.Name.Contains("NationalIdAttribute", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void LdapUserProfile_DoesNotContainNationalIdProperty()
    {
        Assert.DoesNotContain(
            typeof(LdapUserProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("NationalId", StringComparison.Ordinal));
    }
}
