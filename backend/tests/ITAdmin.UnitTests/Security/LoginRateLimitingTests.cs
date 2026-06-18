using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ITAdmin.Api.Security;

namespace ITAdmin.UnitTests.Security;

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

    [Fact]
    public async Task ReadUserNameAsync_returns_null_when_declared_content_length_exceeds_limit()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"userName":"x"}"""));
        ctx.Request.ContentLength = (64 * 1024) + 1;

        var userName = await LoginRateLimiting.ReadUserNameAsync(ctx.Request, CancellationToken.None);

        Assert.Null(userName);
    }

    [Fact]
    public async Task ReadUserNameAsync_returns_null_without_throwing_for_oversized_body_with_unknown_content_length()
    {
        var ctx = new DefaultHttpContext();
        var padding = new string('a', (64 * 1024) + 512);
        var body = Encoding.UTF8.GetBytes($$"""{"padding":"{{padding}}","userName":"x"}""");
        // Non-seekable so EnableBuffering's bufferLimit applies while reading.
        ctx.Request.Body = new NonSeekableStream(new MemoryStream(body));
        ctx.Request.ContentLength = null;

        var userName = await LoginRateLimiting.ReadUserNameAsync(ctx.Request, CancellationToken.None);

        Assert.Null(userName);
    }

    [Fact]
    public async Task ReadUserNameAsync_returns_null_for_missing_user_name_property()
    {
        var ctx = new DefaultHttpContext();
        var body = Encoding.UTF8.GetBytes("""{"password":"x","rememberMe":false}""");
        ctx.Request.Body = new MemoryStream(body);
        ctx.Request.ContentLength = body.Length;

        var userName = await LoginRateLimiting.ReadUserNameAsync(ctx.Request, CancellationToken.None);

        Assert.Null(userName);
        Assert.Equal(0, ctx.Request.Body.Position);
    }

    [Fact]
    public async Task ReadUserNameAsync_reads_user_name_from_non_seekable_body()
    {
        var ctx = new DefaultHttpContext();
        var body = Encoding.UTF8.GetBytes("""{"userName":"mete.ciftci","password":"x"}""");
        ctx.Request.Body = new NonSeekableStream(new MemoryStream(body));
        ctx.Request.ContentLength = body.Length;

        var userName = await LoginRateLimiting.ReadUserNameAsync(ctx.Request, CancellationToken.None);

        Assert.Equal("mete.ciftci", userName);
    }

    [Fact]
    public async Task ReadUserNameAsync_returns_null_when_body_read_and_rewind_both_fail()
    {
        var ctx = new DefaultHttpContext();
        // Claims to be seekable so EnableBuffering leaves it unwrapped, then fails on both
        // read and rewind. The method must swallow both and fall back to null.
        ctx.Request.Body = new FaultyStream();
        ctx.Request.ContentLength = 10;

        var userName = await LoginRateLimiting.ReadUserNameAsync(ctx.Request, CancellationToken.None);

        Assert.Null(userName);
    }

    [Fact]
    public async Task ReadUserNameAsync_does_not_throw_when_rewind_is_not_supported()
    {
        var ctx = new DefaultHttpContext();
        var body = Encoding.UTF8.GetBytes("""{"userName":"x"}""");
        ctx.Request.Body = new NonRewindableSeekableStream(new MemoryStream(body));
        ctx.Request.ContentLength = body.Length;

        var userName = await LoginRateLimiting.ReadUserNameAsync(ctx.Request, CancellationToken.None);

        Assert.Equal("x", userName);
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FaultyStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 10;
        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException("Rewind is not supported.");
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Body stream is broken.");
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class NonRewindableSeekableStream(MemoryStream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException("Rewind is not supported.");
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
