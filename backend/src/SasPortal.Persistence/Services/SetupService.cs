using Microsoft.EntityFrameworkCore;
using SasPortal.Application.Abstractions.Services;
using SasPortal.Persistence.Context;

namespace SasPortal.Persistence.Services;

public sealed class SetupService(AppDbContext context) : ISetupService
{
    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        var hasAnyUser = await context.PortalUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        var isSetupCompleted = await context.ApplicationSettings
            .AsNoTracking()
            .AnyAsync(x =>
                    x.Key == "Setup:IsCompleted" &&
                    x.Value == "true" &&
                    x.IsActive &&
                    !x.IsDeleted,
                cancellationToken);

        return !hasAnyUser || !isSetupCompleted;
    }
}
