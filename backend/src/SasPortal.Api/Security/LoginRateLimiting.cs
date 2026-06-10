using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SasPortal.Api.Security;

/// <summary>
/// Brute-force brake for <c>POST /api/auth/login</c> built on ASP.NET Core rate limiting.
/// Attempts are partitioned by client IP + submitted user name so a single attacker cannot
/// exhaust the budget of other users, while a distributed guess against one account is still
/// throttled per source address. Only the login endpoint opts into this policy; refresh,
/// logout and <c>/me</c> are intentionally not rate limited here.
/// </summary>
public static class LoginRateLimiting
{
    public const string PolicyName = "login";

    /// <summary>HttpContext.Items key carrying the user name extracted from the login body.</summary>
    public const string UserNameItemKey = "LoginRateLimit:UserName";

    private const string LoginPath = "/api/auth/login";
    private const long MaxInspectedBodyBytes = 64 * 1024;

    public const string GenericRateLimitedMessage = "Too many login attempts. Please try again later.";
    public const string RateLimitedErrorCode = "RateLimited";

    private static readonly byte[] RejectionBody = JsonSerializer.SerializeToUtf8Bytes(new
    {
        isSuccess = false,
        message = GenericRateLimitedMessage,
        errorCode = RateLimitedErrorCode,
    });

    public static IServiceCollection AddLoginRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = LoginRateLimitOptions.Load(configuration);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = OnRejectedAsync;
            limiter.AddPolicy(PolicyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds),
                        QueueLimit = options.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    }));
        });

        return services;
    }

    public static string ResolvePartitionKey(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var userName = context.Items.TryGetValue(UserNameItemKey, out var value) ? value as string : null;
        return CreatePartitionKey(ipAddress, userName);
    }

    public static string CreatePartitionKey(string? ipAddress, string? userName)
    {
        var normalizedIp = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Trim();
        var normalizedUser = string.IsNullOrWhiteSpace(userName) ? "-" : userName.Trim().ToLowerInvariant();
        return $"{normalizedIp}|{normalizedUser}";
    }

    public static bool IsLoginRequest(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) &&
        request.Path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the user name from the buffered login body so the partition key can include it.
    /// This is a best-effort inspection: oversized, malformed, unreadable or aborted bodies
    /// yield <c>null</c> instead of an exception, the partition key falls back to IP-only and
    /// model binding handles the bad body later. The password is never materialized or logged.
    /// </summary>
    public static async Task<string?> ReadUserNameAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        // Declared oversized bodies are skipped up front; bodies without a Content-Length are
        // still read, but EnableBuffering's bufferLimit caps them so an attacker cannot stream
        // an unbounded body through this inspection (the overflow surfaces as an IOException
        // which is swallowed below).
        if (request.ContentLength is > MaxInspectedBodyBytes)
        {
            return null;
        }

        try
        {
            request.EnableBuffering(bufferThreshold: (int)MaxInspectedBodyBytes, bufferLimit: MaxInspectedBodyBytes);

            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("userName", out var userNameElement) &&
                userNameElement.ValueKind == JsonValueKind.String)
            {
                return userNameElement.GetString();
            }

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client aborted the request; let the pipeline observe the aborted request instead
            // of failing here with a 500.
            return null;
        }
        catch (Exception exception) when (IsSafeBodyReadException(exception))
        {
            return null;
        }
        finally
        {
            TryRewindBody(request);
        }
    }

    /// <summary>
    /// Body read failures that must degrade to IP-only partitioning instead of bubbling up
    /// as a 500: malformed JSON, transport/buffering errors (including the buffer-limit
    /// overflow), unsupported or already-disposed body streams.
    /// </summary>
    private static bool IsSafeBodyReadException(Exception exception) =>
        exception is JsonException
            or IOException
            or InvalidDataException
            or NotSupportedException
            or ObjectDisposedException;

    private static void TryRewindBody(HttpRequest request)
    {
        try
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }
        catch (Exception exception) when (IsSafeBodyReadException(exception))
        {
            // Rewind is best-effort; a broken stream will fail again in model binding.
        }
    }

    private static ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        // Application log only: the user name is not a secret and the event is needed to
        // observe brute-force pressure. Passwords or tokens are never logged here.
        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(LoginRateLimiting).FullName!);

        logger.LogWarning(
            "Login rate limit exceeded. Ip: {IpAddress}, UserName: {UserName}",
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            httpContext.Items.TryGetValue(UserNameItemKey, out var userName) ? userName as string ?? "-" : "-");

        httpContext.Response.ContentType = "application/json";
        return new ValueTask(httpContext.Response.Body.WriteAsync(RejectionBody, cancellationToken).AsTask());
    }
}

/// <summary>
/// Extracts the login user name into <see cref="HttpContext.Items"/> before the rate limiter
/// runs, so the partition key can combine IP and user name. Only buffers the body for the
/// login endpoint; every other request passes through untouched.
/// </summary>
public sealed class LoginRateLimitPartitioningMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (LoginRateLimiting.IsLoginRequest(context.Request))
        {
            context.Items[LoginRateLimiting.UserNameItemKey] =
                await LoginRateLimiting.ReadUserNameAsync(context.Request, context.RequestAborted);
        }

        await next(context);
    }
}

public static class LoginRateLimitPartitioningMiddlewareExtensions
{
    public static IApplicationBuilder UseLoginRateLimitPartitioning(this IApplicationBuilder builder) =>
        builder.UseMiddleware<LoginRateLimitPartitioningMiddleware>();
}
