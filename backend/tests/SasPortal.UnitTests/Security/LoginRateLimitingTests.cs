using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SasPortal.Api.Security;

namespace SasPortal.UnitTests.Security;

public sealed class LoginRateLimitingTests
{
    [Fact]
    public void Options_defaults_are_safe_but_do_not_lock_development()
    {
        var options = LoginRateLimitOptions.Load(new ConfigurationBuilder().Build());

        Assert.Equal(LoginRateLimitOptions.DefaultPermitLimit, options.PermitLimit);
        Assert.Equal(LoginRateLimitOptions.DefaultWindowSeconds, options.WindowSeconds);
        Assert.Equal(LoginRateLimitOptions.DefaultQueueLimit, options.QueueLimit);
        Assert.True(options.PermitLimit > 1);
    }

    [Fact]
    public void Options_load_reads_configuration_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:LoginRateLimit:PermitLimit"] = "3",
                ["Security:LoginRateLimit:WindowSeconds"] = "30",
                ["Security:LoginRateLimit:QueueLimit"] = "1",
            })
            .Build();

        var options = LoginRateLimitOptions.Load(configuration);

        Assert.Equal(3, options.PermitLimit);
        Assert.Equal(30, options.WindowSeconds);
        Assert.Equal(1, options.QueueLimit);
    }

    [Fact]
    public void Options_sanitize_falls_back_for_invalid_values()
    {
        var options = new LoginRateLimitOptions
        {
            PermitLimit = 0,
            WindowSeconds = -5,
            QueueLimit = -1,
        }.Sanitize();

        Assert.Equal(LoginRateLimitOptions.DefaultPermitLimit, options.PermitLimit);
        Assert.Equal(LoginRateLimitOptions.DefaultWindowSeconds, options.WindowSeconds);
        Assert.Equal(LoginRateLimitOptions.DefaultQueueLimit, options.QueueLimit);
    }

    [Theory]
    [InlineData("198.51.100.7", "Mete.Ciftci", "198.51.100.7|mete.ciftci")]
    [InlineData("198.51.100.7", null, "198.51.100.7|-")]
    [InlineData(null, "user", "unknown|user")]
    [InlineData(" 198.51.100.7 ", "  USER  ", "198.51.100.7|user")]
    public void CreatePartitionKey_combines_ip_and_normalized_user_name(
        string? ipAddress,
        string? userName,
        string expectedKey)
    {
        Assert.Equal(expectedKey, LoginRateLimiting.CreatePartitionKey(ipAddress, userName));
    }

    [Theory]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("POST", "/api/auth/refresh", false)]
    [InlineData("POST", "/api/auth/logout", false)]
    [InlineData("GET", "/api/auth/me", false)]
    [InlineData("GET", "/api/auth/login", false)]
    [InlineData("POST", "/api/users", false)]
    public void IsLoginRequest_only_matches_login_post(string method, string path, bool expected)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;

        Assert.Equal(expected, LoginRateLimiting.IsLoginRequest(ctx.Request));
    }

    [Fact]
    public async Task ReadUserNameAsync_extracts_user_name_and_rewinds_body()
    {
        var ctx = new DefaultHttpContext();
        var body = Encoding.UTF8.GetBytes("""{"userName":"mete.ciftci","password":"x"}""");
        ctx.Request.Body = new MemoryStream(body);
        ctx.Request.ContentLength = body.Length;

        var userName = await LoginRateLimiting.ReadUserNameAsync(ctx.Request, CancellationToken.None);

        Assert.Equal("mete.ciftci", userName);
        Assert.Equal(0, ctx.Request.Body.Position);

        using var reader = new StreamReader(ctx.Request.Body);
        var replayed = await reader.ReadToEndAsync();
        Assert.Contains("mete.ciftci", replayed);
    }

    [Fact]
    public async Task ReadUserNameAsync_returns_null_for_malformed_body()
    {
        var ctx = new DefaultHttpContext();
        var body = Encoding.UTF8.GetBytes("not json at all");
        ctx.Request.Body = new MemoryStream(body);
        ctx.Request.ContentLength = body.Length;

        var userName = await LoginRateLimiting.ReadUserNameAsync(ctx.Request, CancellationToken.None);

        Assert.Null(userName);
        Assert.Equal(0, ctx.Request.Body.Position);
    }
}
