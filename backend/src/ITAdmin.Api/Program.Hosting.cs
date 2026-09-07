using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Configuration;
using ITAdmin.Api.Extensions;
using ITAdmin.Api.HostedServices;
using ITAdmin.Api.Middlewares;
using ITAdmin.Api.Security;
using ITAdmin.Api.HostAgent;
using ITAdmin.Application;
using ITAdmin.Application.Common.Models;
using ITAdmin.Infrastructure;
using ITAdmin.Persistence;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;

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

        // Machine secrets (DPAPI under ProgramData) load before prefixed env vars so an operator
        // can still override a single value via ITADMIN_* for break-glass scenarios, while the
        // durable store remains the installer's source of truth for DB password and JWT key.
        builder.Configuration.AddITAdminMachineSecrets();
        builder.Configuration.AddITAdminPrefixedEnvironmentVariables();

        builder.Host.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration.ReadFrom.Configuration(context.Configuration);
        });

        builder.Services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();
        builder.Services.AddSingleton<IHostAgentClient, NamedPipeHostAgentClient>();

        // IIS / reverse proxy support: honor X-Forwarded-For / X-Forwarded-Proto only from
        // proxies declared in configuration (safe loopback-only default when config is empty).
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            ForwardedHeadersSetup.Apply(options, builder.Configuration));

        // HTTPS redirect is opt-in and applied by the Host Agent (Settings -> HTTPS), which sets
        // ITADMIN_Https__RedirectEnabled / ITADMIN_Https__Port on the app pool and recycles it.
        // Until then the site stays plain HTTP so a fresh install is reachable without a certificate.
        var httpsRedirectEnabled = builder.Configuration.GetValue("Https:RedirectEnabled", false);
        var httpsPort = builder.Configuration.GetValue("Https:Port", 443);
        if (httpsRedirectEnabled)
        {
            builder.Services.AddHttpsRedirection(options => options.HttpsPort = httpsPort);

            // HTTPS is operator-toggleable from Settings -> HTTPS. A 30-day HSTS max-age (the
            // framework default) would strand every already-connected browser on https for a
            // month after someone disables it. One day still protects the steady state while
            // keeping "disable HTTPS" actually recoverable.
            builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(1));
        }

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

        if (app.Environment.IsProduction() && httpsRedirectEnabled)
        {
            app.UseHsts();
        }

        if (httpsRedirectEnabled)
        {
            app.UseHttpsRedirection();
        }
        app.UseSecurityHeaders();
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // User uploads (branding logo/favicon) live outside the versioned build directory so they
        // survive an update - the build only ever replaces wwwroot. When Uploads:Root is set
        // (production, via ITADMIN_Uploads__Root) serve /uploads from there; otherwise the default
        // static-files handler still covers wwwroot/uploads for local development.
        var uploadsRoot = app.Configuration.GetValue<string>("Uploads:Root");
        if (!string.IsNullOrWhiteSpace(uploadsRoot))
        {
            Directory.CreateDirectory(uploadsRoot);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(uploadsRoot),
                RequestPath = "/uploads",
            });
        }

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
