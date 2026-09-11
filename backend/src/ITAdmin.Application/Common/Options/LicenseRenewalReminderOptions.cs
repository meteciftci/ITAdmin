namespace ITAdmin.Application.Common.Options;

public sealed class LicenseRenewalReminderOptions
{
    public const string SectionName = "LicenseRenewalReminders";

    public bool WorkerEnabled { get; set; } = true;
    public int ScanIntervalMinutes { get; set; } = 360;
}
