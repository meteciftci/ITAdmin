using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SasPortal.Application.Abstractions.Security;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Infrastructure.Security;
using SasPortal.Infrastructure.Services;

namespace SasPortal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataProtection()
            .SetApplicationName("SAS Portal");

        services.AddScoped<ISecretProtector, SecretProtector>();
        services.AddScoped<ILdapService, LdapService>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}
