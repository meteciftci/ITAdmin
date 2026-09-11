using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using AppModels = ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicenseManagementSettingsAuthorizationTests
{
    [Fact]
    public void RequestDefaults_UsesOperationalPermissionsWithoutExposingSettingsPermission()
    {
        var method = typeof(LicenseManagementSettingsController)
            .GetMethod(nameof(LicenseManagementSettingsController.GetRequestDefaults));
        var attribute = Assert.Single(
            method!.GetCustomAttributes(typeof(RequireAnyPermissionAttribute), inherit: true)
                .Cast<RequireAnyPermissionAttribute>());

        Assert.Equal(
            $"{RequireAnyPermissionAttribute.PolicyPrefix}{LicenseManagementPermissions.ManageRequests}|{LicenseManagementPermissions.FulfillRequests}",
            attribute.Policy);
    }

    [Fact]
    public async Task RequestDefaults_ReturnsOnlyCurrencyAndVatDefaults()
    {
        var controller = new LicenseManagementSettingsController(new StubSettingsService());

        var response = await controller.GetRequestDefaults(CancellationToken.None);

        var body = Assert.IsType<LicenseRequestDefaultsResponse>(
            Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal("EUR", body.DefaultCurrency);
        Assert.True(body.DefaultVatIncluded);
    }

    private sealed class StubSettingsService : ILicenseManagementSettingsService
    {
        public Task<AppModels.LicenseManagementSettingsModel> GetSettingsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppModels.LicenseManagementSettingsModel(
                "EUR",
                true,
                30,
                "private@example.test",
                "private-cc@example.test",
                null,
                null,
                0,
                0,
                null,
                "private notes",
                null,
                null));

        public Task<AppModels.UpdateLicenseManagementSettingsResult> UpdateSettingsAsync(
            AppModels.UpdateLicenseManagementSettingsRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
