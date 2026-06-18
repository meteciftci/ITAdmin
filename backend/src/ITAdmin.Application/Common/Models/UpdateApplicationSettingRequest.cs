using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.Models;

public sealed record UpdateApplicationSettingRequest(
    string Key,
    string? Value,
    SettingValueType ValueType);
