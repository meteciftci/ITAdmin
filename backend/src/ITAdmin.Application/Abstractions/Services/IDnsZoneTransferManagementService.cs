using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsZoneTransferManagementService
{
    Task<DnsZoneTransferOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<DnsZoneTransferOperationModel> UpdateAsync(DnsZoneTransferMutationCommand command, CancellationToken cancellationToken = default);
}
