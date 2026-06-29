namespace ITAdmin.Application.Common.Models.LicenseManagement;

public sealed record DirectoryUserLookupReadinessResult(
    bool IsReady,
    string Reason,
    string? Message);
