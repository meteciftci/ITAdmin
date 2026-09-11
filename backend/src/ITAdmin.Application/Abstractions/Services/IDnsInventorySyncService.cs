using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsInventorySyncService
{
    Task<DnsAdministrationResult<DnsSyncJobModel>> EnqueueAsync(
        Guid serverId, DnsActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}
