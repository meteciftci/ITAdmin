namespace ITAdmin.Application.Common.Models;

public sealed record AdAttributeMappingResult(
    bool IsSuccess,
    string MessageKey,
    AdAttributeMappingItem? Mapping);
