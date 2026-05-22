using Microsoft.AspNetCore.Authorization;
using SasPortal.Api.Authorization;
using SasPortal.Api.Extensions;
using SasPortal.Api.HostedServices;
using SasPortal.Application;
using SasPortal.Application.Common.Models;
using SasPortal.Infrastructure;
using SasPortal.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SasPortal.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Text;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, loggerConfiguration) =>
    {
        loggerConfiguration.ReadFrom.Configuration(context.Configuration);
    });

    // Add services to the container.

    builder.Services.AddControllers();
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
    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    builder.Services.AddScoped<IAuthorizationHandler, AnyPermissionAuthorizationHandler>();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    builder.Services.AddHostedService<NotificationOutboxWorker>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    app.UseGlobalExceptionHandling();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseDefaultFiles();
    app.UseStaticFiles();

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

    Log.Information("Starting SAS Portal API");
    app.Run();
}
catch (HostAbortedException) when (EF.IsDesignTime)
{
    // Expected during EF Core design-time operations (dotnet ef).
}
catch (HostAbortedException exception)
{
    Console.Error.WriteLine(exception);
    Log.Fatal(exception, "SAS Portal API host was aborted");
    Environment.ExitCode = 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Log.Fatal(exception, "SAS Portal API terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
