using ITAdmin.Application.Common.Models.LicenseManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDirectoryUserLookupReadinessService
{
    Task<DirectoryUserLookupReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}
