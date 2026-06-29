using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Domain.Enums;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicenseManagementApiJsonBindingTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Deserialize_CreateLicensedProductRequest_AcceptsNamedUserStringEnum()
    {
        const string json = """
            {
              "name": "Photoshop",
              "vendorCompanyId": null,
              "category": "Grafik",
              "defaultLicenseType": "NamedUser",
              "description": null,
              "isActive": true,
              "notes": null
            }
            """;

        var request = JsonSerializer.Deserialize<CreateLicensedProductRequest>(json, ApiJsonOptions);

        Assert.NotNull(request);
        Assert.Equal("Photoshop", request.Name);
        Assert.Equal(LicenseType.NamedUser, request.DefaultLicenseType);
    }

    [Fact]
    public void Deserialize_CreateLicensedProductRequest_AcceptsNullDefaultLicenseType()
    {
        const string json = """
            {
              "name": "Photoshop",
              "vendorCompanyId": null,
              "category": null,
              "defaultLicenseType": null,
              "description": null,
              "isActive": true,
              "notes": null
            }
            """;

        var request = JsonSerializer.Deserialize<CreateLicensedProductRequest>(json, ApiJsonOptions);

        Assert.NotNull(request);
        Assert.Null(request.DefaultLicenseType);
    }

    [Fact]
    public void Deserialize_CreateLicensePurchaseRequest_AcceptsStringEnums()
    {
        const string json = """
            {
              "purchaseType": "DirectPurchase",
              "title": "Office purchase",
              "description": null,
              "purchaseDate": null,
              "tenderNumber": null,
              "tenderDate": null,
              "directPurchaseNumber": "DT-1",
              "dmoOrderNumber": null,
              "ebysNumber": null,
              "ebysDate": null,
              "invoiceNumber": null,
              "invoiceDate": null,
              "contractNumber": null,
              "contractStartDate": null,
              "contractEndDate": null,
              "supplierCompanyId": null,
              "supportCompanyId": null,
              "actualTotalCost": null,
              "currency": null,
              "vatIncluded": null,
              "notes": null,
              "status": "Draft"
            }
            """;

        var request = JsonSerializer.Deserialize<CreateLicensePurchaseRequest>(json, ApiJsonOptions);

        Assert.NotNull(request);
        Assert.Equal(LicensePurchaseType.DirectPurchase, request.PurchaseType);
        Assert.Equal(LicensePurchaseStatus.Draft, request.Status);
    }

    [Fact]
    public void Deserialize_CreateLicensePackageRequest_AcceptsStringEnums()
    {
        const string json = """
            {
              "purchaseId": "00000000-0000-0000-0000-000000000001",
              "productId": "00000000-0000-0000-0000-000000000002",
              "licenseType": "NamedUser",
              "quantity": 10,
              "startDate": null,
              "endDate": null,
              "isPerpetual": false,
              "renewalRequired": false,
              "renewalDate": null,
              "serialNumber": null,
              "licenseKey": null,
              "licenseAccountEmail": null,
              "licensePortalUrl": null,
              "licenseNotes": null,
              "isActive": true,
              "status": "Active"
            }
            """;

        var request = JsonSerializer.Deserialize<CreateLicensePackageRequest>(json, ApiJsonOptions);

        Assert.NotNull(request);
        Assert.Equal(LicenseType.NamedUser, request.LicenseType);
        Assert.Equal(LicensePackageStatus.Active, request.Status);
    }

    [Fact]
    public void Deserialize_CreateLicensedProductRequest_RejectsInvalidEnumValue()
    {
        const string json = """
            {
              "name": "Photoshop",
              "defaultLicenseType": "NotARealType",
              "isActive": true
            }
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CreateLicensedProductRequest>(json, ApiJsonOptions));
    }
}
