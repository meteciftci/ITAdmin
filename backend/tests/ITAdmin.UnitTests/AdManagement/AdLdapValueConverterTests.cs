using ITAdmin.Application.Common.AdManagement;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdLdapValueConverterTests
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(1, true)]
    public void FromAdFileTime_ReturnsNullForInvalidValues(long fileTime, bool? expectValue)
    {
        var result = AdLdapValueConverter.FromAdFileTime(fileTime);
        if (expectValue is null)
        {
            Assert.Null(result);
            return;
        }

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(512, true)]
    [InlineData(514, false)]
    [InlineData(null, true)]
    public void IsAccountEnabled_UsesDisabledBit(int? userAccountControl, bool expected)
    {
        var result = AdLdapValueConverter.IsAccountEnabled(userAccountControl);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(512, false, true)]
    [InlineData(514, true, false)]
    [InlineData(512, true, false)]
    public void ApplyAccountDisabledFlag_TogglesDisabledBit(int userAccountControl, bool disabled, bool expectedEnabled)
    {
        var updated = AdLdapValueConverter.ApplyAccountDisabledFlag(userAccountControl, disabled);
        Assert.Equal(expectedEnabled, AdLdapValueConverter.IsAccountEnabled(updated));
    }

    [Theory]
    [InlineData(0L, false)]
    [InlineData(1L, true)]
    [InlineData(null, false)]
    public void IsAccountLockedOut_WhenLockoutTimePositive(long? lockoutTime, bool expected)
    {
        var result = AdLdapValueConverter.IsAccountLockedOut(lockoutTime);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(0, 20)]
    [InlineData(500, 100)]
    [InlineData(25, 25)]
    public void ClampPageSize_EnforcesBounds(int input, int expected)
    {
        var result = AdLdapValueConverter.ClampPageSize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseGeneralizedTime_ParsesAdTimestamp()
    {
        var parsed = AdLdapValueConverter.ParseGeneralizedTime("20240115103000Z");
        Assert.NotNull(parsed);
        Assert.Equal(DateTimeKind.Utc, parsed!.Value.UtcDateTime.Kind);
    }
}
