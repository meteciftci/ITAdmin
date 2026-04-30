namespace SasPortal.Application.Abstractions.Services;

public interface ISetupService
{
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);
}
