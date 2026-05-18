namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdAttributeMappingResponse(
    Guid Id,
    string LogicalField,
    string DisplayName,
    string AttributeName,
    bool IsEnabled,
    bool IsEditable,
    bool IsSensitive,
    bool IsSearchable,
    string ValidationType,
    string MaskingStrategy,
    int SortOrder);
