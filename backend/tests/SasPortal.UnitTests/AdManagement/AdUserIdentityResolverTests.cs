using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdUserIdentityResolverTests
{
    private static readonly Guid UserGuid = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

    [Fact]
    public void ResolvePublicUserId_WhenObjectGuidPresent_ReturnsGuid()
    {
        var id = UserGuid.ToString("D");

        Assert.Equal(id, AdUserIdentityResolver.ResolvePublicUserId(id, "mete.test"));
    }

    [Fact]
    public void ResolvePublicUserId_WhenObjectGuidMissing_ReturnsSamAccountName_NotDn()
    {
        const string dn = "CN=mete.test,OU=Users,DC=corp,DC=local";

        var id = AdUserIdentityResolver.ResolvePublicUserId(null, "mete.test");

        Assert.Equal("mete.test", id);
        Assert.NotEqual(dn, id);
        Assert.False(AdUserIdentityResolver.LooksLikeDistinguishedName(id));
    }

    [Fact]
    public void ResolveAuditEntityId_NeverUsesDistinguishedName()
    {
        const string dn = "CN=mete.test,OU=Users,DC=corp,DC=local";

        var entityId = AdUserIdentityResolver.ResolveAuditEntityId(dn, "mete.test");

        Assert.Equal("mete.test", entityId);
        Assert.DoesNotContain("CN=", entityId, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveAuditEntityId_WhenGuidPresent_ReturnsGuid()
    {
        var guid = UserGuid.ToString("D");

        Assert.Equal(guid, AdUserIdentityResolver.ResolveAuditEntityId(guid, "mete.test"));
    }
}
