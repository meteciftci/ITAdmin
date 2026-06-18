using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ITAdmin.IntegrationTests.Infrastructure;

public sealed class IntegrationTestHostConfigurationTests
{
    [Fact]
    public void Quiet_logging_configuration_sets_fatal_serilog_and_critical_microsoft_levels()
    {
        var configuration = IntegrationTestHostConfiguration.QuietLoggingConfiguration();

        Assert.Equal("Fatal", configuration["Serilog:MinimumLevel:Default"]);
        Assert.Equal("Fatal", configuration["Serilog:MinimumLevel:Override:Microsoft"]);
        Assert.Equal("Fatal", configuration["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"]);
        Assert.Equal("Fatal", configuration["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore"]);
        Assert.Equal("Fatal", configuration["Serilog:MinimumLevel:Override:Npgsql"]);
        Assert.Equal("Fatal", configuration["Serilog:MinimumLevel:Override:ITAdmin"]);
        Assert.Equal("Critical", configuration["Logging:LogLevel:Default"]);
        Assert.Equal("Critical", configuration["Logging:LogLevel:Microsoft.EntityFrameworkCore"]);
        Assert.Equal("Critical", configuration["Logging:LogLevel:Npgsql"]);
    }

    [Fact]
    public void Web_root_path_points_to_an_existing_directory()
    {
        var webRootPath = IntegrationTestHostConfiguration.WebRootPath;

        Assert.False(string.IsNullOrWhiteSpace(webRootPath));
        Assert.True(Directory.Exists(webRootPath));
    }

    [Fact]
    public void Factory_host_uses_configured_web_root_path()
    {
        using var factory = new ITAdminWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        Assert.Equal(IntegrationTestHostConfiguration.WebRootPath, environment.WebRootPath);
        Assert.True(Directory.Exists(environment.WebRootPath));
    }
}
