using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnssecManagementService
{
    Task<DnssecOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<DnssecOperationModel> MutateAsync(DnssecMutationCommand command, CancellationToken cancellationToken = default);
}
