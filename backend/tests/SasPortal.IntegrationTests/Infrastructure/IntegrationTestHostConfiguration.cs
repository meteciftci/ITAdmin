using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace SasPortal.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only host settings that keep integration test output quiet without changing
/// production, development, or staging logging behavior.
/// </summary>
internal static class IntegrationTestHostConfiguration
{
    private static readonly Lazy<string> WebRootDirectory = new(CreateWebRootDirectory);

    internal static string WebRootPath => WebRootDirectory.Value;

    internal static WebApplicationOptions CreateWebApplicationOptions(string[] args) =>
        new()
        {
            Args = args,
            EnvironmentName = "Testing",
            ApplicationName = typeof(Program).Assembly.GetName().Name!,
            WebRootPath = WebRootPath,
        };

    internal static IReadOnlyDictionary<string, string?> CreateBaseConfiguration() =>
        new Dictionary<string, string?>(QuietLoggingConfiguration())
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=127.0.0.1;Port=5432;Database=sasportal_integration_test;Username=test;Password=test",
            ["Jwt:Key"] = "integration-test-signing-key-with-at-least-32-characters",
            ["Jwt:Issuer"] = "SASPortal",
            ["Jwt:Audience"] = "SASPortal.Client",
            ["NotificationOutbox:WorkerEnabled"] = "false",
        };

    internal static void Apply(WebApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(CreateBaseConfiguration());
    }

    internal static IReadOnlyDictionary<string, string?> QuietLoggingConfiguration() =>
        new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Critical",
            ["Logging:LogLevel:Microsoft"] = "Critical",
            ["Logging:LogLevel:Microsoft.AspNetCore"] = "Critical",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Critical",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"] = "Critical",
            ["Logging:LogLevel:Npgsql"] = "Critical",

            ["Serilog:MinimumLevel:Default"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore.Hosting"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore.StaticFiles"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command"] = "Fatal",
            ["Serilog:MinimumLevel:Override:Npgsql"] = "Fatal",
            ["Serilog:MinimumLevel:Override:SasPortal"] = "Fatal",
        };

    private static string CreateWebRootDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "sasportal-integration-test-wwwroot");
        Directory.CreateDirectory(path);
        return path;
    }
}
