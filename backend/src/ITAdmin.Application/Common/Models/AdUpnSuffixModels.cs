namespace ITAdmin.Application.Common.Models;

public sealed record AdUpnSuffixItem(string Value, string Source);

public sealed record AdUpnSuffixesResult(
    bool IsSuccess,
    string MessageKey,
    IReadOnlyList<AdUpnSuffixItem>? Items,
    string? Warning = null,
    AdDirectoryFailureKind? FailureKind = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
