using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicensePackageSecretBackfillService(
    AppDbContext context,
    ISecretProtector secretProtector)
{
    public async Task<int> ProtectPlaintextLicenseKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var packages = await context.LicensePackages
            .Where(package =>
                package.LicenseKey != null
                && package.LicenseKey != ""
                && !package.LicenseKeyIsEncrypted)
            .ToListAsync(cancellationToken);

        foreach (var package in packages)
        {
            package.LicenseKey = secretProtector.Protect(package.LicenseKey!);
            package.LicenseKeyIsEncrypted = true;
        }

        if (packages.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return packages.Count;
    }
}
