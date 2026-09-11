using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsServerConnectionTestService
{
    Task<DnsAdministrationResult<DnsServerConnectionTestModel>> TestAsync(
        Guid serverId, DnsActorContext actor, CancellationToken cancellationToken = default);
}
