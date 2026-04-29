using SasPortal.Api.Extensions;
using SasPortal.Application;
using SasPortal.Infrastructure;
using SasPortal.Persistence;
using Microsoft.Extensions.Hosting;
using Serilog;

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
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    app.UseGlobalExceptionHandling();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("Starting SAS Portal API");
    app.Run();
}
catch (HostAbortedException)
{
    // Expected during EF Core design-time operations.
}
catch (Exception exception)
{
    Log.Fatal(exception, "SAS Portal API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
