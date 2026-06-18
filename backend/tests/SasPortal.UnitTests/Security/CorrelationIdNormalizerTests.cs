using SasPortal.Application.Common.Security;

namespace SasPortal.UnitTests.Security;

public sealed class CorrelationIdNormalizerTests
{
    [Fact]
    public void Resolve_GeneratesId_WhenHeaderMissing()
    {
        var resolved = CorrelationIdNormalizer.Resolve(null);

        Assert.False(string.IsNullOrWhiteSpace(resolved));
        Assert.True(Guid.TryParse(resolved, out _));
    }

    [Fact]
    public void Resolve_UsesValidHeaderValue()
    {
        var expected = "abc-123-def";

        var resolved = CorrelationIdNormalizer.Resolve(expected);

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Normalize_StripsControlCharacters()
    {
        var normalized = CorrelationIdNormalizer.Normalize("abc\r\n123");

        Assert.Equal("abc123", normalized);
    }

    [Fact]
    public void Normalize_ReturnsNull_WhenValueTooLong()
    {
        var normalized = CorrelationIdNormalizer.Normalize(new string('a', CorrelationIdConstants.MaxLength + 1));

        Assert.Null(normalized);
    }

    [Fact]
    public void Resolve_GeneratesId_WhenHeaderTooLong()
    {
        var resolved = CorrelationIdNormalizer.Resolve(new string('a', CorrelationIdConstants.MaxLength + 1));

        Assert.True(Guid.TryParse(resolved, out _));
    }
}
