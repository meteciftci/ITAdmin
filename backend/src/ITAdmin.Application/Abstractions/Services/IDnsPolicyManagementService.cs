using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsPolicyManagementService
{
    Task<DnsPolicyOperationModel> GetAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<DnsPolicyOperationModel> MutateAsync(DnsPolicyMutationCommand command, CancellationToken cancellationToken = default);
}
