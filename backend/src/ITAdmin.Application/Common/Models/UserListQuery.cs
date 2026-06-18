namespace ITAdmin.Application.Common.Models;

public sealed record UserListQuery(
    string? Search,
    bool? IsActive,
    int PageNumber,
    int PageSize);
