using System.Text.Json;
using System.Text.Json.Serialization;
using ITAdmin.Api.Contracts.LicenseManagement;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicenseManagementApiJsonBindingTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Deserialize_CreateLicensedProductRequest_AcceptsBrandAndCategoryId()
    {
        const string json = """
            {
              "name": "Photoshop",
              "brand": "Adobe",
              "categoryId": "00000000-0000-0000-0000-000000000001",
              "description": "Design suite",
              "isActive": true
            }
            """;

        var request = JsonSerializer.Deserialize<CreateLicensedProductRequest>(json, ApiJsonOptions);

        Assert.NotNull(request);
        Assert.Equal("Photoshop", request.Name);
        Assert.Equal("Adobe", request.Brand);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), request.CategoryId);
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
        Assert.Equal(Domain.Enums.LicensePurchaseType.DirectPurchase, request.PurchaseType);
        Assert.Equal(Domain.Enums.LicensePurchaseStatus.Draft, request.Status);
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
        Assert.Equal(Domain.Enums.LicenseType.NamedUser, request.LicenseType);
        Assert.Equal(Domain.Enums.LicensePackageStatus.Active, request.Status);
    }

    [Fact]
    public void Deserialize_DirectoryUserLookupReadinessResponse_DoesNotExposeSettingsFields()
    {
        const string json = """
            {
              "isReady": false,
              "reason": "AdManagementNotConfigured",
              "message": "AD kullanıcı arama için AD Yönetim bağlantısı yapılandırılmalıdır."
            }
            """;

        var response = JsonSerializer.Deserialize<DirectoryUserLookupReadinessResponse>(json, ApiJsonOptions);

        Assert.NotNull(response);
        Assert.False(response.IsReady);
        Assert.Equal("AdManagementNotConfigured", response.Reason);
        Assert.DoesNotContain("baseDn", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serviceAccount", json, StringComparison.OrdinalIgnoreCase);
    }
}
