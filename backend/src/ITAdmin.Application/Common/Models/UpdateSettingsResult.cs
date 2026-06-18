namespace ITAdmin.Application.Common.Models;

public sealed record UpdateSettingsResult(
    bool IsSuccess,
    string Message,
    SettingsOverview? Settings);
