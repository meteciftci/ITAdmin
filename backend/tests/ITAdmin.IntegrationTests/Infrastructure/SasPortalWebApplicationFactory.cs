using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ITAdmin.IntegrationTests.Infrastructure;

public sealed class ITAdminWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?>? _extraConfiguration;

    public ITAdminWebApplicationFactory()
        : this(extraConfiguration: null)
    {
    }

    internal ITAdminWebApplicationFactory(IReadOnlyDictionary<string, string?>? extraConfiguration)
    {
        _extraConfiguration = extraConfiguration;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var app = Program.CreateWebApplication(
            [],
            applicationBuilder =>
            {
                IntegrationTestHostConfiguration.Apply(applicationBuilder);
                applicationBuilder.WebHost.UseTestServer();

                if (_extraConfiguration is { Count: > 0 })
                {
                    applicationBuilder.Configuration.AddInMemoryCollection(
                        _extraConfiguration.ToDictionary(x => x.Key, x => x.Value));
                }
            },
            IntegrationTestHostConfiguration.CreateWebApplicationOptions([]));

        app.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return app;
    }
}
