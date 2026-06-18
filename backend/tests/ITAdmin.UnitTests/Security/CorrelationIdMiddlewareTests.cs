using Microsoft.AspNetCore.Http;
using ITAdmin.Api.Middlewares;
using ITAdmin.Application.Common.Security;

namespace ITAdmin.UnitTests.Security;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Invoke_AddsCorrelationIdHeader_WhenRequestHeaderMissing()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new CorrelationIdMiddleware(_ =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, Microsoft.Extensions.Logging.Abstractions.NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.ContainsKey(CorrelationIdConstants.HeaderName));
        var headerValue = context.Response.Headers[CorrelationIdConstants.HeaderName].ToString();
        Assert.False(string.IsNullOrWhiteSpace(headerValue));
        Assert.Equal(headerValue, CorrelationIdMiddleware.TryGetCorrelationId(context));
    }

    [Fact]
    public async Task Invoke_EchoesNormalizedRequestHeader()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers[CorrelationIdConstants.HeaderName] = "client-trace-01";

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal("client-trace-01", context.Response.Headers[CorrelationIdConstants.HeaderName].ToString());
    }

    [Fact]
    public void ResolveCorrelationId_GeneratesId_ForInvalidHeader()
    {
        var headers = new HeaderDictionary
        {
            [CorrelationIdConstants.HeaderName] = new string('x', CorrelationIdConstants.MaxLength + 5),
        };

        var resolved = CorrelationIdMiddleware.ResolveCorrelationId(headers);

        Assert.True(Guid.TryParse(resolved, out _));
    }
}
