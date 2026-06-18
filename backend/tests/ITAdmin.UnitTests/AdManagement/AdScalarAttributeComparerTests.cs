using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdScalarAttributeComparerTests
{
    [Fact]
    public void HasChanged_ReturnsFalse_WhenValuesEqualIgnoreCase()
    {
        Assert.False(AdScalarAttributeComparer.HasChanged("Ali", "ali"));
    }

    [Fact]
    public void HasChanged_ReturnsFalse_WhenBothEmpty()
    {
        Assert.False(AdScalarAttributeComparer.HasChanged(null, "  "));
    }

    [Fact]
    public void HasChanged_ReturnsTrue_WhenValuesDiffer()
    {
        Assert.True(AdScalarAttributeComparer.HasChanged("old", "new"));
    }
}
