using ITAdmin.Application.Common.Models;

namespace ITAdmin.Application.Abstractions.Services;

public interface ISecurityLogService
{
    Task<PagedResult<SecurityLogListItem>> GetSecurityLogsAsync(
        SecurityLogListQuery query,
        CancellationToken cancellationToken = default);

    Task<SecurityLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}
