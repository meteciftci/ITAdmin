namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdDeletedObjectRestoreRequestBody(
    string? RestoreTargetMode = null,
    string? TargetPathDistinguishedName = null);
