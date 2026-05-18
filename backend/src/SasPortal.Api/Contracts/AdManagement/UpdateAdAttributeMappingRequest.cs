namespace SasPortal.Api.Contracts.AdManagement;

public sealed record UpdateAdAttributeMappingRequest
{
    public string DisplayName { get; init; } = string.Empty;
    public string AttributeName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public bool IsEditable { get; init; } = true;
    public bool IsSensitive { get; init; }
    public bool IsSearchable { get; init; }
    public string ValidationType { get; init; } = "None";
    public string MaskingStrategy { get; init; } = "None";
    public int SortOrder { get; init; }
}
