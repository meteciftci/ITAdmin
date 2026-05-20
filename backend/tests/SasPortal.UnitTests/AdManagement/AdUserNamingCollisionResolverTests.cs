using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdUserNamingCollisionResolverTests
{
    [Fact]
    public void Resolve_BuildsUpnFromSamAccountNameAndSelectedSuffix()
    {
        var resolved = AdUserNamingCollisionResolver.Resolve(
            "Çağrı",
            "IŞIK",
            null,
            "mugla.bel.tr",
            _ => false);

        Assert.NotNull(resolved);
        Assert.Equal("cagri.isik", resolved!.SamAccountName);
        Assert.Equal("cagri.isik@mugla.bel.tr", resolved.UserPrincipalName);
    }

    [Fact]
    public void Resolve_AppliesSuffixWhenCnCollides()
    {
        var resolved = AdUserNamingCollisionResolver.Resolve(
            "Çağrı",
            "IŞIK",
            null,
            "mugla.bel.tr",
            candidate => candidate.CommonName == "Çağrı IŞIK");

        Assert.NotNull(resolved);
        Assert.Equal("cagri.isik2", resolved!.SamAccountName);
        Assert.Equal("cagri.isik2@mugla.bel.tr", resolved.UserPrincipalName);
        Assert.Equal("Çağrı IŞIK 2", resolved.DisplayName);
    }

    [Fact]
    public void Resolve_ReturnsNullWhenMaxAttemptsExceeded()
    {
        var resolved = AdUserNamingCollisionResolver.Resolve(
            "Test",
            "User",
            null,
            "example.com",
            _ => true,
            maxAttempts: 3);

        Assert.Null(resolved);
    }
}
