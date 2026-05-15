namespace SasPortal.Application.Common.Models;

public sealed record AdAttributeMappingItem(
    Guid Id,
    string LogicalField,
    string DisplayName,
    string AttributeName,
    bool IsEnabled,
    bool IsEditable,
    bool IsSensitive,
    string ValidationType,
    string MaskingStrategy,
    int SortOrder);
