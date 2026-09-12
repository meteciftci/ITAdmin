using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsRecordMutationService
{
    Task<DnsRecordMutationModel> ExecuteAsync(
        DnsRecordMutationCommand command, CancellationToken cancellationToken = default);
}
