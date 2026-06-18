namespace ITAdmin.Application.Common.Options;

public sealed class NotificationOutboxOptions
{
    public const string SectionName = "NotificationOutbox";

    public bool WorkerEnabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
    public int ProcessingLockTimeoutMinutes { get; set; } = 10;
}
