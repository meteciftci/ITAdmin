using SasPortal.Domain.Common;
using SasPortal.Domain.Enums;

namespace SasPortal.Domain.Entities;

public class ApplicationSetting : SoftDeletableEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public SettingValueType ValueType { get; set; }
    public string? Description { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
}
