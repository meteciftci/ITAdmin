namespace ITAdmin.Api.Contracts.Settings;

public sealed record ApplicationSettingResponse(
    string Key,
    string? Value,
    int ValueType,
    string? Description,
    bool IsEncrypted,
    bool IsSystem,
    bool IsActive);
