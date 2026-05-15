using SasPortal.Api.Security;

namespace SasPortal.UnitTests.Security;

public sealed class CsrfTokenGeneratorTests
{
    [Fact]
    public void CreateToken_returns_distinct_non_empty_base64_url_strings()
    {
        var a = CsrfTokenGenerator.CreateToken();
        var b = CsrfTokenGenerator.CreateToken();

        Assert.False(string.IsNullOrWhiteSpace(a));
        Assert.False(string.IsNullOrWhiteSpace(b));
        Assert.NotEqual(a, b);
        Assert.False(a.Contains('+', StringComparison.Ordinal));
        Assert.False(a.Contains('/', StringComparison.Ordinal));
        Assert.False(a.Contains('=', StringComparison.Ordinal));
    }
}
