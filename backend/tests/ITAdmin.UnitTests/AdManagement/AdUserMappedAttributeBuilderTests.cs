using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdUserMappedAttributeBuilderTests
{
    [Fact]
    public void Build_IncludesActiveMapping_WhenValueExists()
    {
        var mappings = new[]
        {
            CreateMapping("mobilePhone", "Cep Telefonu", "extensionAttribute1", isSearchable: true),
        };

        var attributes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["extensionAttribute1"] = ["5551234567"],
        };

        var result = AdUserMappedAttributeBuilder.Build(
            name => attributes.TryGetValue(name, out var values)
                ? values
                : Array.Empty<string>(),
            mappings);

        Assert.Single(result);
        Assert.Equal("Cep Telefonu", result[0].DisplayName);
        Assert.Equal(["5551234567"], result[0].Value);
    }

    [Fact]
    public void Build_IncludesMapping_WhenIsSearchableFalse()
    {
        var mappings = new[]
        {
            CreateMapping("pdks", "PDKS", "employeeNumber", isSearchable: false),
        };

        var attributes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["employeeNumber"] = ["12345"],
        };

        var result = AdUserMappedAttributeBuilder.Build(
            name => attributes.TryGetValue(name, out var values)
                ? values
                : Array.Empty<string>(),
            mappings);

        Assert.Single(result);
        Assert.False(result[0].IsSearchable);
    }

    [Fact]
    public void Build_MasksSensitiveMappingValues()
    {
        var mappings = new[]
        {
            CreateMapping("nationalId", "T.C. Kimlik No", "extensionAttribute2", isSensitive: true, isSearchable: false),
        };

        var attributes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["extensionAttribute2"] = ["12345678901"],
        };

        var result = AdUserMappedAttributeBuilder.Build(
            name => attributes.TryGetValue(name, out var values)
                ? values
                : Array.Empty<string>(),
            mappings);

        Assert.Single(result);
        Assert.Equal(["••••"], result[0].Value);
    }

    [Fact]
    public void Build_ReadsAttributeCaseInsensitive()
    {
        var mappings = new[]
        {
            CreateMapping("mobilePhone", "Cep Telefonu", "extensionAttribute1", isSearchable: true),
        };

        var attributes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["extensionattribute1"] = ["555"],
        };

        var result = AdUserMappedAttributeBuilder.Build(
            name => attributes.TryGetValue(name, out var values)
                ? values
                : Array.Empty<string>(),
            mappings);

        Assert.Single(result);
        Assert.Equal(["555"], result[0].Value);
    }

    [Fact]
    public void Build_IncludesEnabledMappingWithNullValueWhenAdValueEmpty()
    {
        var mappings = new[]
        {
            CreateMapping("mobilePhone", "Cep Telefonu", "extensionAttribute1", isSearchable: true),
        };

        var attributes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["extensionAttribute1"] = ["", "  "],
        };

        var result = AdUserMappedAttributeBuilder.Build(
            name => attributes.TryGetValue(name, out var values)
                ? values
                : Array.Empty<string>(),
            mappings);

        Assert.Single(result);
        Assert.Null(result[0].Value);
        Assert.Equal("mobilePhone", result[0].LogicalField);
        Assert.Equal("Cep Telefonu", result[0].DisplayName);
        Assert.True(result[0].IsEditable);
    }

    [Fact]
    public void Build_IncludesEnabledMappingWithNullValueWhenAttributeMissingInAd()
    {
        var mappings = new[]
        {
            CreateMapping("pdks", "PDKS", "employeeNumber", isSearchable: false),
        };

        var result = AdUserMappedAttributeBuilder.Build(
            _ => Array.Empty<string>(),
            mappings);

        Assert.Single(result);
        Assert.Null(result[0].Value);
        Assert.Equal("pdks", result[0].LogicalField);
    }

    [Fact]
    public void Build_SkipsInvalidAttributeName()
    {
        var mappings = new[]
        {
            CreateMapping("invalid", "Invalid", "bad attr", isSearchable: true),
        };

        var attributes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["bad attr"] = ["value"],
        };

        var result = AdUserMappedAttributeBuilder.Build(
            name => attributes.TryGetValue(name, out var values)
                ? values
                : Array.Empty<string>(),
            mappings);

        Assert.Empty(result);
    }

    private static AdAttributeMappingItem CreateMapping(
        string logicalField,
        string displayName,
        string attributeName,
        bool isSensitive = false,
        bool isSearchable = false,
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
