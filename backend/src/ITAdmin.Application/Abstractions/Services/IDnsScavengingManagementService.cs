using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsScavengingManagementService
{
    Task<DnsScavengingOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<DnsScavengingOperationModel> MutateAsync(DnsScavengingMutationCommand command, CancellationToken cancellationToken = default);
}
