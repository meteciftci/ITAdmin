using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SasPortal.Api.Authorization;
using SasPortal.Application.Common.Security;

namespace SasPortal.UnitTests.Authorization;

public sealed class AnyPermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_SucceedsWhenUserHasAnyRequiredPermission()
    {
        var handler = new AnyPermissionAuthorizationHandler();
        var requirement = new AnyPermissionRequirement(
        [
            "AdManagement.Users.Create",
            "AdManagement.Settings.View",
        ]);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Permission, "AdManagement.Settings.View"),
        ],
        authenticationType: "test"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            user,
            resource: null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceedWithoutMatchingPermission()
    {
        var handler = new AnyPermissionAuthorizationHandler();
        var requirement = new AnyPermissionRequirement(
        [
            "AdManagement.Users.Create",
            "AdManagement.Settings.View",
        ]);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Permission, "Users.View"),
        ],
        authenticationType: "test"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            user,
            resource: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
