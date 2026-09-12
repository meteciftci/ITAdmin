using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsServerSettingsService
{
    Task<DnsServerSettingsOperationModel> GetAsync(
        Guid serverId, CancellationToken cancellationToken = default);
    Task<DnsServerSettingsOperationModel> UpdateAsync(
        DnsServerSettingsCommand command, CancellationToken cancellationToken = default);
    Task<DnsServerSettingsOperationModel> ClearCacheAsync(
        Guid serverId, DnsActorContext actor, CancellationToken cancellationToken = default);
}
