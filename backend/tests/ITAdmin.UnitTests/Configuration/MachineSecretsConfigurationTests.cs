using ITAdmin.Api.Configuration;
using Microsoft.Extensions.Configuration;

namespace ITAdmin.UnitTests.Configuration;

public sealed class MachineSecretsConfigurationTests
{
    [Fact]
    public void AddITAdminMachineSecrets_WhenSecretsRootMissing_DoesNotThrow()
    {
        var previous = Environment.GetEnvironmentVariable(
            MachineSecretsConfigurationExtensions.SecretsRootEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                MachineSecretsConfigurationExtensions.SecretsRootEnvironmentVariable,
                Path.Combine(Path.GetTempPath(), "itadmin-missing-secrets-" + Guid.NewGuid().ToString("N")));

            var configuration = new ConfigurationBuilder()
                .AddITAdminMachineSecrets()
                .Build();

            Assert.Null(configuration["Jwt:Key"]);
            Assert.Null(configuration["ConnectionStrings:DefaultConnection"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                MachineSecretsConfigurationExtensions.SecretsRootEnvironmentVariable,
                previous);
        }
    }

    [Fact]
    public void ProtectedFileName_MatchesInstallerContract() =>
        Assert.Equal("runtime.secrets.dpapi", MachineSecretsConfigurationExtensions.ProtectedFileName);
}
