using SasPortal.Application.Common.AdManagement;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdLdapFilterHelperTests
{
    [Theory]
    [InlineData("*", "\\2a")]
    [InlineData("(", "\\28")]
    [InlineData(")", "\\29")]
    [InlineData("\\", "\\5c")]
    [InlineData("a\0b", "a\\00b")]
    public void EscapeFilterValue_EscapesSpecialCharacters(string input, string expectedFragment)
    {
        var escaped = AdLdapFilterHelper.EscapeFilterValue(input);
        Assert.Contains(expectedFragment, escaped, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatObjectGuidFilter_ProducesBinaryEscapedFilter()
    {
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var filter = AdLdapFilterHelper.FormatObjectGuidFilter(guid);

        Assert.StartsWith("\\", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("*", filter, StringComparison.Ordinal);
    }
}
