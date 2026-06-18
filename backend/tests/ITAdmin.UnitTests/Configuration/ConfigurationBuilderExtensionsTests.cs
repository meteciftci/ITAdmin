using ITAdmin.Api.Extensions;
using ITAdmin.Application.Common.Constants;
using Microsoft.Extensions.Configuration;

namespace ITAdmin.UnitTests.Configuration;

public sealed class ConfigurationBuilderExtensionsTests
{
    private const string JwtKeyVariable = ITAdminEnvironmentVariables.Prefix + "Jwt__Key";
    private const string ConnectionStringVariable = ITAdminEnvironmentVariables.Prefix + "ConnectionStrings__DefaultConnection";
    private const string SetupKeyHashVariable = ITAdminEnvironmentVariables.Prefix + "Setup__SetupKeyHash";
    private const string DataProtectionKeysPathVariable = ITAdminEnvironmentVariables.Prefix + "DataProtection__KeysPath";

    [Fact]
    public void AddITAdminPrefixedEnvironmentVariables_BindsJwtConnectionSetupAndDataProtectionKeys()
    {
        const string jwtKey = "prefixed-jwt-key-with-at-least-32-characters";
        const string connectionString = "Host=127.0.0.1;Port=5432;Database=test;Username=test;Password=test";
        const string setupKeyHash = "sha256:abc";
        const string keysPath = "C:\\ProgramData\\ITAdmin\\DataProtection-Keys";

        SetEnvironmentVariable(JwtKeyVariable, jwtKey);
        SetEnvironmentVariable(ConnectionStringVariable, connectionString);
        SetEnvironmentVariable(SetupKeyHashVariable, setupKeyHash);
        SetEnvironmentVariable(DataProtectionKeysPathVariable, keysPath);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "memory-jwt-key",
                    ["ConnectionStrings:DefaultConnection"] = "memory-connection",
                    ["Setup:SetupKeyHash"] = "sha256:memory",
                    ["DataProtection:KeysPath"] = "C:\\memory",
                })
                .AddITAdminPrefixedEnvironmentVariables()
                .Build();

            Assert.Equal(jwtKey, configuration["Jwt:Key"]);
            Assert.Equal(connectionString, configuration.GetConnectionString("DefaultConnection"));
            Assert.Equal(setupKeyHash, configuration["Setup:SetupKeyHash"]);
            Assert.Equal(keysPath, configuration["DataProtection:KeysPath"]);
        }
        finally
        {
            SetEnvironmentVariable(JwtKeyVariable, null);
            SetEnvironmentVariable(ConnectionStringVariable, null);
            SetEnvironmentVariable(SetupKeyHashVariable, null);
            SetEnvironmentVariable(DataProtectionKeysPathVariable, null);
        }
    }

    [Fact]
    public void AddITAdminPrefixedEnvironmentVariables_DoesNotBreakExistingInMemoryConfiguration_WhenPrefixNotSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "memory-jwt-key-with-at-least-32-characters",
                ["Jwt:Issuer"] = "ITAdmin",
                ["Jwt:Audience"] = "ITAdmin.Client",
            })
            .AddITAdminPrefixedEnvironmentVariables()
            .Build();

        Assert.Equal("memory-jwt-key-with-at-least-32-characters", configuration["Jwt:Key"]);
        Assert.Equal("ITAdmin", configuration["Jwt:Issuer"]);
        Assert.Equal("ITAdmin.Client", configuration["Jwt:Audience"]);
    }

    private static void SetEnvironmentVariable(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
    }
}
