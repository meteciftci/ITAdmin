using SasPortal.Domain.Enums;

namespace SasPortal.Application.Common.Models;

public sealed record UpdateApplicationSettingRequest(
    string Key,
    string? Value,
    SettingValueType ValueType);
