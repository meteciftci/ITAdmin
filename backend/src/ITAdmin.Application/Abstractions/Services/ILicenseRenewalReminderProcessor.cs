namespace ITAdmin.Application.Abstractions.Services;

public sealed record LicenseRenewalReminderRunResult(
    int DuePackageCount,
    int QueuedCount,
    int AlreadyQueuedCount,
    int InvalidRecipientCount,
    string Message);

public interface ILicenseRenewalReminderProcessor
{
    Task<LicenseRenewalReminderRunResult> RunAsync(CancellationToken cancellationToken = default);
}
