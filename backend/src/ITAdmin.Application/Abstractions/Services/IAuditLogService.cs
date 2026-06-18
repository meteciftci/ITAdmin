using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogListItem>> GetAuditLogsAsync(
        AuditLogListQuery query,
        CancellationToken cancellationToken = default);

    Task<AuditLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}
