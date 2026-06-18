using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.UnitTests.Services;

public sealed class AdServiceAccountBindIdentityTests
{
    [Fact]
    public void Build_WhenUserNameContainsBackslash_ReturnsAsIs()
    {
        var identity = AdServiceAccountBindIdentity.Build("DOMAIN\\svc", "OTHER");

        Assert.Equal("DOMAIN\\svc", identity);
    }

    [Fact]
    public void Build_WhenUserNameContainsAtSign_ReturnsAsIs()
    {
        var identity = AdServiceAccountBindIdentity.Build("svc@domain.local", "DOMAIN");

        Assert.Equal("svc@domain.local", identity);
    }

    [Fact]
    public void Build_WhenUserNameIsPlain_AndNetbiosSet_ReturnsDownLevelLogonName()
    {
        var identity = AdServiceAccountBindIdentity.Build("svc", "DOMAIN");

        Assert.Equal("DOMAIN\\svc", identity);
    }

    [Fact]
    public void Build_TrimsUserNameAndNetbios_BeforeComposing()
    {
        var identity = AdServiceAccountBindIdentity.Build(" svc ", " DOMAIN ");

        Assert.Equal("DOMAIN\\svc", identity);
    }

    [Fact]
    public void Build_WhenUserNameIsPlain_AndNetbiosNull_ReturnsUserNameTrimmed()
    {
        var identity = AdServiceAccountBindIdentity.Build("svc", null);

        Assert.Equal("svc", identity);
    }

    [Fact]
    public void Build_WhenUserNameIsPlain_AndNetbiosWhitespace_ReturnsUserNameTrimmed()
    {
        var identity = AdServiceAccountBindIdentity.Build("svc", "   ");

        Assert.Equal("svc", identity);
    }

    [Fact]
    public void Build_WhenUserNameIsNullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, AdServiceAccountBindIdentity.Build(null, "DOMAIN"));
        Assert.Equal(string.Empty, AdServiceAccountBindIdentity.Build("", "DOMAIN"));
        Assert.Equal(string.Empty, AdServiceAccountBindIdentity.Build("   ", "DOMAIN"));
    }
}
