using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Infrastructure.Services;

namespace SasPortal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ILdapService, LdapService>();

        return services;
    }
}
