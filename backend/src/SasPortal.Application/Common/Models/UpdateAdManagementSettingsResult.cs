namespace SasPortal.Application.Common.Models;

public sealed record UpdateAdManagementSettingsResult(
    bool IsSuccess,
    string Message,
    AdManagementSettingsModel? Settings);
