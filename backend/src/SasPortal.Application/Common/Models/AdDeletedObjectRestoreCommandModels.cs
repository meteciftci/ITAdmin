namespace SasPortal.Application.Common.Models;

public sealed record AdDeletedObjectRestoreCommandRequest(
    Guid ObjectGuid,
    string Server,
    AdDeletedObjectRestoreTargetMode RestoreTargetMode,
    string? TargetPathDistinguishedName,
    string? ServiceAccountUserName,
    string? ServiceAccountPassword,
    string? NetbiosDomainName,
    TimeSpan Timeout);

public sealed record AdDeletedObjectRestoreCommandResult(
    bool IsSuccess,
    string CredentialMode,
    long ElapsedMs,
    int? ExitCode,
    string? SanitizedErrorSummary,
    AdDirectoryFailureKind? FailureKind);
