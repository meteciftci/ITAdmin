using ITAdmin.Application.Common.Constants;
using Microsoft.Extensions.Configuration;

namespace ITAdmin.Api.Extensions;

public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds machine-level environment variables prefixed with <see cref="ITAdminEnvironmentVariables.Prefix"/>.
    /// Provider is registered after appsettings and default environment variables so prefixed values
    /// override them in production bootstrap scenarios.
    /// </summary>
    public static IConfigurationBuilder AddITAdminPrefixedEnvironmentVariables(this IConfigurationBuilder builder)
    {
        builder.AddEnvironmentVariables(ITAdminEnvironmentVariables.Prefix);
        return builder;
    }
}
