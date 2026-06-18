using ITAdmin.Application.Common.AdManagement;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.UnitTests.AdManagement;

public sealed class AdCreateUserMappedAttributeValidatorTests
{
    private static readonly IReadOnlyList<AdAttributeMappingItem> Mappings =
    [
        new AdAttributeMappingItem(
            Guid.NewGuid(),
            "mobilePhone",
            "Mobile Phone",
            "mobile",
            IsEnabled: true,
            IsEditable: true,
            IsSensitive: false,
            IsSearchable: false,
            ValidationType: "Phone",
            MaskingStrategy: "None",
            SortOrder: 1),
        new AdAttributeMappingItem(
            Guid.NewGuid(),
            "employeeId",
            "Employee ID",
            "employeeID",
            IsEnabled: true,
            IsEditable: false,
            IsSensitive: false,
            IsSearchable: false,
            ValidationType: "None",
            MaskingStrategy: "None",
            SortOrder: 2),
    ];

    [Fact]
    public void TryValidate_FailsForNonEditableMapping()
    {
        var isValid = AdCreateUserMappedAttributeValidator.TryValidate(
            [new CreateAdUserMappedAttributeRequest("employeeId", "123")],
            Mappings,
            out var messageKey,
            out var messageParams);

        Assert.False(isValid);
        Assert.Equal(AdManagementApiMessageKeys.MappedAttributes.NotEditable, messageKey);
        Assert.Equal("employeeId", messageParams!["logicalField"]);
    }

    [Fact]
    public void TryValidate_FailsForInvalidPhone()
    {
        var isValid = AdCreateUserMappedAttributeValidator.TryValidate(
            [new CreateAdUserMappedAttributeRequest("mobilePhone", "abc")],
            Mappings,
            out var messageKey,
            out _);

        Assert.False(isValid);
        Assert.Equal(AdManagementApiMessageKeys.MappedAttributes.InvalidPhoneFormat, messageKey);
    }

    [Fact]
    public void TryValidate_FailsForForbiddenAttributeName()
    {
        var mappings = new List<AdAttributeMappingItem>(Mappings)
        {
            new AdAttributeMappingItem(
                Guid.NewGuid(),
                "secretField",
                "Secret",
                "unicodePwd",
                IsEnabled: true,
                IsEditable: true,
                IsSensitive: true,
                IsSearchable: false,
                ValidationType: "None",
                MaskingStrategy: "Hidden",
                SortOrder: 3),
        };

        var isValid = AdCreateUserMappedAttributeValidator.TryValidate(
            [new CreateAdUserMappedAttributeRequest("secretField", "x")],
            mappings,
            out var messageKey,
            out _);

        Assert.False(isValid);
        Assert.Equal(AdReservedCoreAttributes.ReservedAttributeMappingMessageKey, messageKey);
    }

    [Fact]
    public void TryValidate_SucceedsForValidEditableMapping()
    {
        var isValid = AdCreateUserMappedAttributeValidator.TryValidate(
            [new CreateAdUserMappedAttributeRequest("mobilePhone", "+905551112233")],
            Mappings,
            out var messageKey,
            out _);

        Assert.True(isValid);
        Assert.Empty(messageKey);
    }
}
