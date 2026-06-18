using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Api.Authorization;
using SasPortal.Api.Extensions;
using SasPortal.Api.HostedServices;
using SasPortal.Api.Middlewares;
using SasPortal.Api.Security;
using SasPortal.Application;
using SasPortal.Application.Common.Models;
using SasPortal.Infrastructure;
using SasPortal.Persistence;
using Serilog;
using System.Text;

public partial class Program
{
    public static WebApplication CreateWebApplication(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null,
        WebApplicationOptions? options = null)
    {
        var builder = options is null
            ? WebApplication.CreateBuilder(args)
            : WebApplication.CreateBuilder(options);

        configureBuilder?.Invoke(builder);

        builder.Host.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration.ReadFrom.Configuration(context.Configuration);
        });

        builder.Services.AddControllers();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

        // IIS / reverse proxy support: honor X-Forwarded-For / X-Forwarded-Proto only from
        // proxies declared in configuration (safe loopback-only default when config is empty).
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            ForwardedHeadersSetup.Apply(options, builder.Configuration));

        builder.Services.AddLoginRateLimiting(builder.Configuration);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddPersistence(builder.Configuration);
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Key))
        {
            throw new InvalidOperationException("Jwt:Key must be provided via user-secrets or environment variables.");
        }

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = JwtBearerCookieTokenResolver.OnMessageReceived,
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        builder.Services.AddScoped<ForbiddenAccessSecurityLogger>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddScoped<IAuthorizationHandler, AnyPermissionAuthorizationHandler>();
        builder.Services.AddOpenApi();
        builder.Services.AddHealthChecks();
        builder.Services.AddHostedService<NotificationOutboxWorker>();

        var app = builder.Build();

        // Forwarded headers must run first so downstream middleware (HTTPS redirect, cookies,
        // rate limiting, logging) observe the real client IP and original scheme.
        app.UseForwardedHeaders();

        app.UseCorrelationId();
        app.UseGlobalExceptionHandling();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        if (app.Environment.IsProduction())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseSecurityHeaders();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Explicit routing so the endpoint (and its [EnableRateLimiting] metadata) is resolved
        // before the rate limiter runs; the limiter only engages for the login endpoint.
        app.UseRouting();

        app.UseLoginRateLimitPartitioning();
        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseCsrfProtection();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health");

        app.MapFallback(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var webRootPath = app.Environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var indexPath = Path.Combine(webRootPath, "index.html");
            if (!File.Exists(indexPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(indexPath);
        });

        return app;
    }
}
