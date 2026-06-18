namespace ITAdmin.Api.Contracts.Settings;

public sealed record UpdateApplicationSettingsRequest
{
    public IReadOnlyList<UpdateApplicationSettingRequest> Items { get; init; } = [];
}
