using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsZoneMutationService
{
    Task<DnsZoneMutationModel> ExecuteAsync(
        DnsZoneMutationCommand command, CancellationToken cancellationToken = default);
}
