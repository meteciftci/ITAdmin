using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.DnsManagement;

namespace ITAdmin.Application.Abstractions.Services;

public interface IDnsOperationLogService
{
    Task<PagedResult<DnsOperationLogListItemModel>> GetAsync(
        DnsOperationLogQuery query, CancellationToken cancellationToken = default);
    Task<DnsOperationLogDetailModel?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default);
}
