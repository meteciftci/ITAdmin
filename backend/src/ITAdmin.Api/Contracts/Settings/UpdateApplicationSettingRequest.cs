namespace ITAdmin.Api.Contracts.Settings;

public sealed record UpdateApplicationSettingRequest
{
    public string Key { get; init; } = string.Empty;
    public string? Value { get; init; }
    public int ValueType { get; init; }
}
