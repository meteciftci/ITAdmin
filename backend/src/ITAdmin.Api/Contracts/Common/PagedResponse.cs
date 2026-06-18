namespace ITAdmin.Api.Contracts.Common;

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
