using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SasPortal.IntegrationTests.Infrastructure;

public sealed class SasPortalWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?>? _extraConfiguration;

    public SasPortalWebApplicationFactory()
        : this(extraConfiguration: null)
    {
    }

    internal SasPortalWebApplicationFactory(IReadOnlyDictionary<string, string?>? extraConfiguration)
    {
        _extraConfiguration = extraConfiguration;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var app = Program.CreateWebApplication([], applicationBuilder =>
        {
            applicationBuilder.Environment.EnvironmentName = "Testing";
            // The entry assembly is the test host, so controller discovery would find nothing
            // without pointing the application name at the API assembly.
            applicationBuilder.Environment.ApplicationName = typeof(Program).Assembly.GetName().Name!;
            applicationBuilder.WebHost.UseTestServer();
            applicationBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=5432;Database=sasportal_integration_test;Username=test;Password=test",
                ["Jwt:Key"] = "integration-test-signing-key-with-at-least-32-characters",
                ["Jwt:Issuer"] = "SASPortal",
                ["Jwt:Audience"] = "SASPortal.Client",
                ["NotificationOutbox:WorkerEnabled"] = "false",
            });

            if (_extraConfiguration is { Count: > 0 })
            {
                applicationBuilder.Configuration.AddInMemoryCollection(
                    _extraConfiguration.ToDictionary(x => x.Key, x => x.Value));
            }
        });

        app.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return app;
    }
}
