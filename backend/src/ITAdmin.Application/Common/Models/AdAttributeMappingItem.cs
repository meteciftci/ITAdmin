namespace ITAdmin.Application.Common.Models;

public sealed record AdAttributeMappingItem(
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
