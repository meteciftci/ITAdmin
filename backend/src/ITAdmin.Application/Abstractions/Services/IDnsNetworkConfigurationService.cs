using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsNetworkConfigurationService
{
    Task<DnsNetworkOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<DnsNetworkOperationModel> MutateAsync(DnsNetworkMutationCommand command, CancellationToken cancellationToken = default);
}
