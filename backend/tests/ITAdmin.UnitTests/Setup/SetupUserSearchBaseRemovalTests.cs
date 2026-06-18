using System.Reflection;
using ITAdmin.Api.Contracts.Setup;

namespace ITAdmin.UnitTests.Setup;

public sealed class SetupUserSearchBaseRemovalTests
{
    [Fact]
    public void SetupLdapContracts_DoNotContainUserSearchBaseProperty()
    {
        var modelTypes = new[]
        {
            typeof(CompleteSetupLdapSettingsRequest),
            typeof(ValidateLdapRequest),
        };

        foreach (var modelType in modelTypes)
        {
            Assert.DoesNotContain(
                modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.Name == "UserSearchBase");
        }
    }
}
