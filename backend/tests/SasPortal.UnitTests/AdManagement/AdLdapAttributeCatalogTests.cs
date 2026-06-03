using SasPortal.Application.Common.AdManagement;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.AdManagement;

public sealed class AdLdapAttributeCatalogTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    public void IsSearchTermValid_ReturnsFalseForShortOrEmptySearch(string? search)
    {
        Assert.False(AdLdapAttributeCatalog.IsSearchTermValid(search));
    }

    [Fact]
    public void IsSearchTermValid_ReturnsTrueForTwoOrMoreCharacters()
    {
        Assert.True(AdLdapAttributeCatalog.IsSearchTermValid("ab"));
    }

    [Fact]
    public void BuildUserSearchFilter_IncludesDepartmentAndEmployeeFields()
    {
        var filter = AdLdapAttributeCatalog.BuildUserSearchFilter(
            "sales",
            AdUserStatusFilter.Active,
            []);

        Assert.Contains("(department=*sales*)", filter, StringComparison.Ordinal);
        Assert.Contains("(employeeType=*sales*)", filter, StringComparison.Ordinal);
        Assert.Contains("(employeeNumber=*sales*)", filter, StringComparison.Ordinal);
        Assert.Contains("(employeeID=*sales*)", filter, StringComparison.Ordinal);
        Assert.Contains("(l=*sales*)", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSearchableMappingAttributeNames_IncludesEnabledSearchableNonSensitiveMapping()
    {
        var mappings = new[]
        {
            CreateMapping("pdksKartNo", "PDKS", "employeeNumber", isSensitive: false, isSearchable: true),
        };

        var attributes = AdLdapAttributeCatalog.GetSearchableMappingAttributeNames(mappings);

        Assert.Single(attributes);
        Assert.Equal("employeeNumber", attributes[0]);
    }

    [Fact]
    public void GetSearchableMappingAttributeNames_ExcludesNonSearchableMapping()
    {
        var mappings = new[]
        {
            CreateMapping("pdksKartNo", "PDKS", "employeeNumber", isSensitive: false, isSearchable: false),
        };

        var attributes = AdLdapAttributeCatalog.GetSearchableMappingAttributeNames(mappings);

        Assert.Empty(attributes);
    }

    [Fact]
    public void GetSearchableMappingAttributeNames_ExcludesSensitiveMappingEvenWhenSearchableRequested()
    {
        var mappings = new[]
        {
            CreateMapping("secretField", "Secret", "extensionAttribute2", isSensitive: true, isSearchable: true),
        };

        var attributes = AdLdapAttributeCatalog.GetSearchableMappingAttributeNames(mappings);

        Assert.Empty(attributes);
    }

    [Fact]
    public void BuildUserSearchFilter_IncludesSearchableMappingAttribute()
    {
        var mappings = new[]
        {
            CreateMapping("pdksKartNo", "PDKS", "employeeNumber", isSensitive: false, isSearchable: true),
            CreateMapping("secretField", "Secret", "extensionAttribute2", isSensitive: true, isSearchable: false),
        };

        var filter = AdLdapAttributeCatalog.BuildUserSearchFilter(
            "123",
            AdUserStatusFilter.All,
            AdLdapAttributeCatalog.GetSearchableMappingAttributeNames(mappings));

        Assert.Contains("(employeeNumber=*123*)", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("(extensionAttribute2=*123*)", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSearchableMappingAttributeNames_SkipsInvalidAttributeNames()
    {
        var mappings = new[]
        {
            CreateMapping("valid", "Valid", "extensionAttribute1", isSensitive: false, isSearchable: true),
            CreateMapping("invalid", "Invalid", "bad attr", isSensitive: false, isSearchable: true),
        };

        var attributes = AdLdapAttributeCatalog.GetSearchableMappingAttributeNames(mappings);

        Assert.Single(attributes);
        Assert.Equal("extensionAttribute1", attributes[0]);
    }

    [Fact]
    public void MergeAttributeNames_DeduplicatesCaseInsensitive()
    {
        var merged = AdLdapAttributeCatalog.MergeAttributeNames(
            ["employeeNumber", "EMPLOYEENUMBER"],
            ["employeeNumber"]);

        Assert.Single(merged);
    }

    [Fact]
    public void BuildDetailLdapAttributeNames_IncludesCoreBasicAndAccountFields()
    {
        var attributes = AdLdapAttributeCatalog.BuildDetailLdapAttributeNames([]);

        Assert.Contains(attributes, name => name.Equals("givenName", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, name => name.Equals("sn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, name => name.Equals("memberOf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, name => name.Equals("accountExpires", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, name => name.Equals("badPwdCount", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(attributes, name => name.Equals("badPasswordTime", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(attributes, name => name.Equals("employeeType", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(attributes, name => name.Equals("extensionAttribute1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildDetailLdapAttributeNames_IncludesActiveMappingAttributes()
    {
        var mappings = new[]
        {
            CreateMapping("pdks", "PDKS", "employeeNumber", isSensitive: false, isSearchable: false, isEnabled: true),
        };

        var attributes = AdLdapAttributeCatalog.BuildDetailLdapAttributeNames(mappings);

        Assert.Contains(attributes, name => name.Equals("employeeNumber", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("extensionAttribute1", true)]
    [InlineData("mobile", true)]
    [InlineData("bad attr", false)]
    [InlineData("unicodePwd", false)]
    public void IsValidAttributeName_ValidatesLdapDisplayNameFormat(string attributeName, bool expected)
    {
        Assert.Equal(expected, AdLdapAttributeCatalog.IsValidAttributeName(attributeName));
    }

    private static AdAttributeMappingItem CreateMapping(
        string logicalField,
        string displayName,
        string attributeName,
        bool isSensitive,
        bool isSearchable,
        bool isEnabled = true) =>
        new(
            Guid.NewGuid(),
            logicalField,
            displayName,
            attributeName,
            isEnabled,
            true,
            isSensitive,
            isSearchable,
            "None",
            "None",
            0);
}
