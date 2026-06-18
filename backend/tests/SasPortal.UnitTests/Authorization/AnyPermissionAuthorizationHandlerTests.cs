using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using SasPortal.Api.Authorization;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Security;

namespace SasPortal.UnitTests.Authorization;

public sealed class AnyPermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_SucceedsWhenUserHasAnyRequiredPermission()
    {
        var handler = CreateHandler(out _);
        var requirement = new AnyPermissionRequirement(
        [
            PermissionCodes.AdManagement.Users.Create,
            PermissionCodes.AdManagement.Settings.View,
        ]);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(CustomClaimTypes.Permission, PermissionCodes.AdManagement.Settings.View),
        ],
        authenticationType: "test"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            user,
            resource: new DefaultHttpContext());

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceedWithoutMatchingPermission()
    {
        var handler = CreateHandler(out var writer);
        var requirement = new AnyPermissionRequirement(
        [
            PermissionCodes.AdManagement.Users.Create,
            PermissionCodes.AdManagement.Settings.View,
        ]);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
            new Claim(ClaimTypes.Name, "limited.user"),
            new Claim(CustomClaimTypes.Permission, PermissionCodes.Users.View),
        ],
        authenticationType: "test"));
        var context = new AuthorizationHandlerContext(
            [requirement],
            user,
            resource: new DefaultHttpContext());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        var entry = Assert.Single(writer.Entries);
        Assert.Equal(SecurityLogEventTypes.ForbiddenAccess, entry.EventType);
        Assert.Contains(PermissionCodes.AdManagement.Users.Create, entry.Description, StringComparison.Ordinal);
        Assert.Contains(PermissionCodes.AdManagement.Settings.View, entry.Description, StringComparison.Ordinal);
        Assert.Contains("(any)", entry.Description, StringComparison.Ordinal);
    }

    private static AnyPermissionAuthorizationHandler CreateHandler(out FakeSecurityLogWriter writer)
    {
        writer = new FakeSecurityLogWriter();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var forbiddenLogger = new ForbiddenAccessSecurityLogger(writer, accessor, NullLogger<ForbiddenAccessSecurityLogger>.Instance);
        return new AnyPermissionAuthorizationHandler(forbiddenLogger);
    }

    private sealed class FakeSecurityLogWriter : ISecurityLogWriter
    {
        public List<SecurityLogWriteRequest> Entries { get; } = [];

        public Task TryWriteAsync(SecurityLogWriteRequest request, CancellationToken cancellationToken = default)
        {
            Entries.Add(request);
            return Task.CompletedTask;
        }
    }
}
