using SasPortal.Application.Common.Models;

namespace SasPortal.Application.Abstractions.Services;

public interface ISecurityLogService
{
    Task<PagedResult<SecurityLogListItem>> GetSecurityLogsAsync(
        SecurityLogListQuery query,
        CancellationToken cancellationToken = default);

    Task<SecurityLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}
