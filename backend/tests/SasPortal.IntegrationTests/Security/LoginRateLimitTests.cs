using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SasPortal.IntegrationTests.Infrastructure;

namespace SasPortal.IntegrationTests.Security;

public sealed class LoginRateLimitTests : IDisposable
{
    private const int PermitLimit = 2;

    private readonly SasPortalWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LoginRateLimitTests()
    {
        _factory = new SasPortalWebApplicationFactory(new Dictionary<string, string?>
        {
            ["Security:LoginRateLimit:PermitLimit"] = PermitLimit.ToString(),
            ["Security:LoginRateLimit:WindowSeconds"] = "300",
            ["Security:LoginRateLimit:QueueLimit"] = "0",
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Fact]
    public async Task Login_attempts_over_limit_return_429_with_generic_body()
    {
        // The integration database is unreachable, so each attempt fails server-side;
        // the rate limiter still consumes one permit per attempt, which is exactly the
        // brute-force behavior under test.
        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            var allowed = await PostLoginAsync("alice");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var rejected = await PostLoginAsync("alice");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("Too many login attempts", body);
        Assert.DoesNotContain("alice", body);
    }

    [Fact]
    public async Task Different_user_names_use_separate_rate_limit_partitions()
    {
        for (var attempt = 0; attempt <= PermitLimit; attempt++)
        {
            await PostLoginAsync("bob");
        }

        var blockedForBob = await PostLoginAsync("bob");
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedForBob.StatusCode);

        var otherUser = await PostLoginAsync("carol");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherUser.StatusCode);
    }

    [Fact]
    public async Task Refresh_and_logout_endpoints_are_not_login_rate_limited()
    {
        for (var attempt = 0; attempt <= PermitLimit; attempt++)
        {
            await PostLoginAsync("dave");
        }

        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "x" });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, refresh.StatusCode);

        var logout = await _client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = "x" });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, logout.StatusCode);
    }

    private Task<HttpResponseMessage> PostLoginAsync(string userName) =>
        _client.PostAsJsonAsync("/api/auth/login", new
        {
            userName,
            password = "integration-test-password",
            rememberMe = false,
        });

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
