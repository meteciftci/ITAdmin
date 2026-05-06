using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogListItem>> GetAuditLogsAsync(
        AuditLogListQuery query,
        CancellationToken cancellationToken = default);
}
