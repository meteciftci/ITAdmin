using System.Text.Json;
using SasPortal.Api.Contracts.Common;

namespace SasPortal.Api.Middlewares;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var errorResponse = new ErrorResponse(
                Message: "An unexpected error occurred.",
                Detail: environment.IsDevelopment() ? exception.ToString() : null,
                StatusCode: StatusCodes.Status500InternalServerError,
                TraceId: context.TraceIdentifier);

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
        }
    }
}
