namespace SasPortal.Application.Common.Models;

public sealed record AdUpnSuffixItem(string Value, string Source);

public sealed record AdUpnSuffixesResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<AdUpnSuffixItem>? Items,
    string? Warning = null,
    AdDirectoryFailureKind? FailureKind = null,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
