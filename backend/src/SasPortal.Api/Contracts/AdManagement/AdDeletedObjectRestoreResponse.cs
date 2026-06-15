namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdDeletedObjectRestoreResponse(
    bool Success,
    string Message,
    string? RestoredObjectId,
    string? RestoredObjectType,
    string? RestoredName,
    string? RestoredSamAccountName,
    string? RestoredDistinguishedName,
    string? RestoredLastKnownParent);
