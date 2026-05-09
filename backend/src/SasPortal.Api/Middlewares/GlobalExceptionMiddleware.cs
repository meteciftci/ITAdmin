using System.Text.Json;
using SasPortal.Api.Contracts.Common;
using SasPortal.Persistence.Common;

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
            var traceId = context.TraceIdentifier;
            var isDbConnectivity = DatabaseExceptionClassifier.IsDatabaseConnectivityException(exception);

            if (isDbConnectivity)
            {
                logger.LogError(
                    exception,
                    "Database connectivity exception occurred. TraceId: {TraceId}",
                    traceId);
            }
            else
            {
                logger.LogError(
                    exception,
                    "Unhandled exception occurred. TraceId: {TraceId}",
                    traceId);
            }

            var statusCode = isDbConnectivity
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;

            var message = isDbConnectivity
                ? "Database service is temporarily unavailable."
                : "An unexpected error occurred.";

            // Detail is only included in Development to avoid leaking DB host / user / pg_hba
            // details in production responses.
            var detail = environment.IsDevelopment() ? exception.ToString() : null;

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var errorResponse = new ErrorResponse(
                Message: message,
                Detail: detail,
                StatusCode: statusCode,
                TraceId: traceId);

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
        }
    }
}
