using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsManagementAdministrationService
{
    Task<DnsManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<DnsAdministrationResult<DnsManagementSettingsModel>> UpdateSettingsAsync(UpdateDnsManagementSettingsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DnsCredentialProfileModel>> GetCredentialProfilesAsync(CancellationToken cancellationToken = default);
    Task<DnsAdministrationResult<DnsCredentialProfileModel>> SaveCredentialProfileAsync(SaveDnsCredentialProfileRequest request, CancellationToken cancellationToken = default);
    Task<DnsAdministrationResult<bool>> DeleteCredentialProfileAsync(Guid id, DnsActorContext actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DnsServerModel>> GetServersAsync(CancellationToken cancellationToken = default);
    Task<DnsAdministrationResult<DnsServerModel>> SaveServerAsync(SaveDnsServerRequest request, CancellationToken cancellationToken = default);
}
