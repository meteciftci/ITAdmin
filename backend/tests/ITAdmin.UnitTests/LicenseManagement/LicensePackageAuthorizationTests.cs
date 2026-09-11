using System.Security.Claims;
using ITAdmin.Api.Contracts.LicenseManagement;
using ITAdmin.Api.Controllers;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using ITAdmin.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AppModels = ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.UnitTests.LicenseManagement;

public sealed class LicensePackageAuthorizationTests
{
    [Fact]
    public void ScopedLicensePermission_ImpliesModuleViewOnly()
    {
        var granted = new[] { PermissionCodes.LicenseManagement.ManageRequests };

        Assert.True(LicenseManagementPermissionRules.IsSatisfiedBy(
            PermissionCodes.LicenseManagement.View,
            granted));
        Assert.False(LicenseManagementPermissionRules.IsSatisfiedBy(
            PermissionCodes.LicenseManagement.ManagePurchases,
            granted));
        Assert.False(LicenseManagementPermissionRules.IsSatisfiedBy(
            PermissionCodes.LicenseManagement.ViewSensitiveData,
            granted));
        Assert.False(LicenseManagementPermissionRules.IsSatisfiedBy(
            PermissionCodes.LicenseManagement.View,
            new[] { PermissionCodes.LicenseManagement.ManageSettings }));
        Assert.False(LicenseManagementPermissionRules.IsSatisfiedBy(
            PermissionCodes.LicenseManagement.View,
            new[] { PermissionCodes.LicenseManagement.ViewSensitiveData }));
    }

    [Fact]
    public async Task GetPackageById_WithBaseView_RedactsConfidentialFields()
    {
        var controller = NewController(PermissionCodes.LicenseManagement.View);

        var response = await controller.GetPackageById(Guid.NewGuid(), CancellationToken.None);

        var body = Assert.IsType<LicensePackageDetailResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.False(body.SensitiveDataVisible);
        Assert.Null(body.SerialNumber);
        Assert.Null(body.LicenseKey);
        Assert.Null(body.LicenseAccountEmail);
        Assert.Null(body.LicensePortalUrl);
        Assert.Null(body.LicenseNotes);
    }

    [Theory]
    [InlineData(PermissionCodes.LicenseManagement.ViewSensitiveData)]
    [InlineData(PermissionCodes.LicenseManagement.ManagePurchases)]
    public async Task GetPackageById_WithConfidentialAccess_ReturnsConfidentialFields(string permission)
    {
        var controller = NewController(permission);

        var response = await controller.GetPackageById(Guid.NewGuid(), CancellationToken.None);

        var body = Assert.IsType<LicensePackageDetailResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.True(body.SensitiveDataVisible);
        Assert.Equal("KEY-SECRET", body.LicenseKey);
        Assert.Equal("SERIAL-1", body.SerialNumber);
        Assert.Equal("licenses@example.test", body.LicenseAccountEmail);
        Assert.Equal("https://licenses.example.test", body.LicensePortalUrl);
        Assert.Equal("confidential note", body.LicenseNotes);
    }

    private static LicensePackagesController NewController(string permission)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(CustomClaimTypes.Permission, permission) },
            "test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new LicensePackagesController(new StubPackageService())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private sealed class StubPackageService : ILicensePackageService
    {
        public Task<AppModels.LicensePackageDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<AppModels.LicensePackageDetail?>(new(
                id,
                Guid.NewGuid(),
                "Purchase",
                Guid.NewGuid(),
                "Product",
                LicenseType.NamedUser,
                10,
                1,
                9,
                null,
                null,
                true,
                false,
                null,
                "SERIAL-1",
                "KEY-SECRET",
                "licenses@example.test",
                "https://licenses.example.test",
                "confidential note",
                true,
                LicensePackageStatus.Active,
                DateTime.UtcNow,
                "tester",
                null,
                null));

        public Task<PagedResult<AppModels.LicensePackageListItem>> GetListAsync(
            AppModels.LicensePackageListQuery query,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AppModels.LicensePackageOperationResult> CreateAsync(
            AppModels.CreateLicensePackageRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AppModels.LicensePackageOperationResult> UpdateAsync(
            AppModels.UpdateLicensePackageRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AppModels.LicensePackageOperationResult> UpdateStatusAsync(
            AppModels.UpdateLicensePackageStatusRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
