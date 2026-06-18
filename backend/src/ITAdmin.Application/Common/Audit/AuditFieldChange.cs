namespace ITAdmin.Application.Common.Audit;

public sealed class AuditFieldChange
{
    public required string FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public bool IsSensitive { get; init; }
    public AuditChangeDisplayMode DisplayMode { get; init; } = AuditChangeDisplayMode.OldNew;
}
