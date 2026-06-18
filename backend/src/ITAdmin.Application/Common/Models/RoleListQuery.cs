namespace ITAdmin.Application.Common.Models;

public sealed record RoleListQuery(
    string? Search,
    bool? IsActive,
    bool? IsSystem,
    int PageNumber,
    int PageSize);
