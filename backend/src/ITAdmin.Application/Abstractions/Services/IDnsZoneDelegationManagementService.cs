using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsZoneDelegationManagementService
{
    Task<DnsZoneDelegationOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<DnsZoneDelegationOperationModel> MutateAsync(DnsZoneDelegationMutationCommand command, CancellationToken cancellationToken = default);
}
