namespace SasPortal.Application.Abstractions.Services;

public sealed record AdDeletedObjectRestoreReadinessPowerShellProbeRequest(
    string? Server,
    string? DomainFqdn,
    string? ServiceAccountUserName,
    string? ServiceAccountPassword,
    string? NetbiosDomainName,
    TimeSpan Timeout);

public sealed record AdDeletedObjectRestoreReadinessPowerShellProbeResult(
    bool IsSuccess,
    string? ErrorSummary,
    string? Details);

public interface IAdDeletedObjectRestoreReadinessPowerShellProbe
{
    Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckActiveDirectoryModuleAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken = default);

    Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckRestoreAdObjectCommandAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken = default);

    Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckRecycleBinFeatureAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken = default);

    Task<AdDeletedObjectRestoreReadinessPowerShellProbeResult> CheckAdwsReadAsync(
        AdDeletedObjectRestoreReadinessPowerShellProbeRequest request,
        CancellationToken cancellationToken = default);
}
