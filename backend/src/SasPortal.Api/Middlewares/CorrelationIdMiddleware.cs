using Microsoft.Extensions.Primitives;
using SasPortal.Application.Common.Security;
using Serilog.Context;

namespace SasPortal.Api.Middlewares;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers);
        context.Items[CorrelationIdConstants.HttpContextItemKey] = correlationId;
        context.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    public static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        headers.TryGetValue(CorrelationIdConstants.HeaderName, out StringValues headerValues);
        return CorrelationIdNormalizer.Resolve(headerValues.FirstOrDefault());
    }

    public static string? TryGetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdConstants.HttpContextItemKey, out var value)
            && value is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return null;
    }
}
