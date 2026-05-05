namespace SasPortal.Application.Common.Models;

public sealed record PermissionListQuery(
    string? Search,
    bool? IsActive,
    int PageNumber,
    int PageSize);
