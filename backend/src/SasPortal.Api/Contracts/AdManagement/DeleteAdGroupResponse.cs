namespace SasPortal.Api.Contracts.AdManagement;

public sealed record DeleteAdGroupResponse(
    bool Success,
    string Message,
    string? DeletedGroupId,
    string? MessageKey = null,
    IReadOnlyDictionary<string, object>? MessageParams = null);
