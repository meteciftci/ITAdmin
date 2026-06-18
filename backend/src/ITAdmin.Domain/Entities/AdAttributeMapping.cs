using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public class AdAttributeMapping : AuditableEntity
{
    public string LogicalField { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsEditable { get; set; } = true;
    public bool IsSensitive { get; set; }
    public bool IsSearchable { get; set; }
    public string ValidationType { get; set; } = "None";
    public string MaskingStrategy { get; set; } = "None";
    public int SortOrder { get; set; }
}
