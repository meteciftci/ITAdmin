using SasPortal.Domain.Enums;

namespace SasPortal.Application.Common.Models;

public sealed record ApplicationSettingItem(
    string Key,
    string? Value,
    SettingValueType ValueType,
    string? Description,
    bool IsEncrypted,
    bool IsSystem,
    bool IsActive);
