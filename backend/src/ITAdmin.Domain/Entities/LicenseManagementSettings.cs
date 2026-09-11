using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public class LicenseManagementSettings : AuditableEntity
{
    public string DefaultCurrency { get; set; } = "TRY";
    public bool DefaultVatIncluded { get; set; }
    public int DefaultRenewalReminderDays { get; set; } = 60;
    public string? DefaultRenewalRecipients { get; set; }
    public string? DefaultRenewalCcRecipients { get; set; }
    public DateTime? LastRenewalReminderRunAt { get; set; }
    public string? LastRenewalReminderStatus { get; set; }
    public int LastRenewalReminderDueCount { get; set; }
    public int LastRenewalReminderQueuedCount { get; set; }
    public string? LastRenewalReminderMessage { get; set; }
    public string? Notes { get; set; }
}
