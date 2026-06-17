namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdDeletedObjectRestoreResponse(
    bool Success,
    string MessageKey,
    string? RestoredObjectId,
    string? RestoredObjectType,
    string? RestoredName,
    string? RestoredSamAccountName,
    string? RestoredDistinguishedName,
    string? RestoredLastKnownParent,
    IReadOnlyDictionary<string, object>? MessageParams = null);
