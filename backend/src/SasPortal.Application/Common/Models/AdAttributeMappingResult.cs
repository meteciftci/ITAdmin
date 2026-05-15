namespace SasPortal.Application.Common.Models;

public sealed record AdAttributeMappingResult(
    bool IsSuccess,
    string Message,
    AdAttributeMappingItem? Mapping);
