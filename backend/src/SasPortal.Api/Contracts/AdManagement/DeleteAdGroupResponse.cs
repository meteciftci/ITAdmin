namespace SasPortal.Api.Contracts.AdManagement;

public sealed record DeleteAdGroupResponse(
    bool Success,
    string MessageKey,
    string? DeletedGroupId,
    IReadOnlyDictionary<string, object>? MessageParams = null);
