using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SasPortal.IntegrationTests.Infrastructure;

public sealed class SasPortalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var app = Program.CreateWebApplication([], applicationBuilder =>
        {
            applicationBuilder.Environment.EnvironmentName = "Testing";
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
        });

        app.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return app;
    }
}
