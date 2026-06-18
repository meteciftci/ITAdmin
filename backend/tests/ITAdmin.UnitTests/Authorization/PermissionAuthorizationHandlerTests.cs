using System.Net;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using ITAdmin.Api.Authorization;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Security;

namespace ITAdmin.UnitTests.Authorization;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_DoesNotWriteSecurityLog_WhenUserHasPermission()
    {
        var (handler, writer, _) = CreateHandler();
        var requirement = new PermissionRequirement(PermissionCodes.Users.View);
        var user = CreateAuthenticatedUser(
            userId: Guid.NewGuid(),
            userName: "allowed.user",
            permissions: [PermissionCodes.Users.View]);
        var context = CreateContext(requirement, user);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Empty(writer.Entries);
    }

    [Fact]
    public async Task HandleRequirementAsync_WritesForbiddenAccessSecurityLog_WhenPermissionMissing()
    {
        var (handler, writer, httpContext) = CreateHandler();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/users";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        httpContext.Request.Headers.UserAgent = "unit-test-agent";

        var userId = Guid.NewGuid();
        var requirement = new PermissionRequirement(PermissionCodes.Users.View);
        var user = CreateAuthenticatedUser(userId, "denied.user", permissions: [PermissionCodes.Roles.View]);
        var context = CreateContext(requirement, user, httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        var entry = Assert.Single(writer.Entries);
        Assert.Equal(SecurityLogEventTypes.ForbiddenAccess, entry.EventType);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal("denied.user", entry.UserName);
        Assert.Equal("203.0.113.10", entry.IpAddress);
        Assert.Equal("unit-test-agent", entry.UserAgent);
        Assert.Contains(PermissionCodes.Users.View, entry.Description, StringComparison.Ordinal);
        Assert.Contains("GET /api/users", entry.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotWriteSecurityLog_WhenUserIsNotAuthenticated()
    {
        var (handler, writer, _) = CreateHandler();
        var requirement = new PermissionRequirement(PermissionCodes.Users.View);
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = CreateContext(requirement, user);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Empty(writer.Entries);
    }

    [Fact]
    public async Task HandleRequirementAsync_LogsOnlyOncePerRequest_WhenMultipleRequirementsFail()
    {
        var (handler, writer, httpContext) = CreateHandler();
        var user = CreateAuthenticatedUser(Guid.NewGuid(), "denied.user", permissions: []);
        var firstRequirement = new PermissionRequirement(PermissionCodes.Users.View);
        var secondRequirement = new PermissionRequirement(PermissionCodes.Roles.View);

        await handler.HandleAsync(CreateContext(firstRequirement, user, httpContext));
        await handler.HandleAsync(CreateContext(secondRequirement, user, httpContext));

        Assert.Single(writer.Entries);
    }

    [Fact]
    public async Task HandleRequirementAsync_DoesNotThrow_WhenSecurityLogWriterFails()
    {
        var writer = new FakeSecurityLogWriter { ShouldThrow = true };
        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var forbiddenLogger = new ForbiddenAccessSecurityLogger(writer, accessor, NullLogger<ForbiddenAccessSecurityLogger>.Instance);
        var handler = new PermissionAuthorizationHandler(forbiddenLogger);

        var requirement = new PermissionRequirement(PermissionCodes.Users.View);
        var user = CreateAuthenticatedUser(Guid.NewGuid(), "denied.user", permissions: []);
        var context = CreateContext(requirement, user, httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static (PermissionAuthorizationHandler Handler, FakeSecurityLogWriter Writer, DefaultHttpContext HttpContext)
        CreateHandler()
    {
        var writer = new FakeSecurityLogWriter();
        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var forbiddenLogger = new ForbiddenAccessSecurityLogger(writer, accessor, NullLogger<ForbiddenAccessSecurityLogger>.Instance);
        return (new PermissionAuthorizationHandler(forbiddenLogger), writer, httpContext);
    }

    private static AuthorizationHandlerContext CreateContext(
        PermissionRequirement requirement,
        ClaimsPrincipal user,
        HttpContext? httpContext = null) =>
        new(
            [requirement],
            user,
            httpContext ?? new DefaultHttpContext());

    private static ClaimsPrincipal CreateAuthenticatedUser(
        Guid userId,
        string userName,
        IReadOnlyList<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new(ClaimTypes.Name, userName),
        };

        claims.AddRange(permissions.Select(permission => new Claim(CustomClaimTypes.Permission, permission)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private sealed class FakeSecurityLogWriter : ISecurityLogWriter
    {
        public List<SecurityLogWriteRequest> Entries { get; } = [];

        public bool ShouldThrow { get; set; }

        public Task TryWriteAsync(SecurityLogWriteRequest request, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Security log write failed.");
            }

            Entries.Add(request);
            return Task.CompletedTask;
        }
    }
}
