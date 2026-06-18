using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public sealed class NotificationProviderSettings : BaseEntity
{
    public string Channel { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? DisplayName { get; set; }
    public string? PublicSettingsJson { get; set; }
    public string? EncryptedSecretSettingsJson { get; set; }
    public DateTimeOffset? LastValidatedAt { get; set; }
    public string? LastValidationStatus { get; set; }
    public string? LastValidationMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
