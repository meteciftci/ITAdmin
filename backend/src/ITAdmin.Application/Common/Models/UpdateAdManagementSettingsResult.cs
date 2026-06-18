namespace ITAdmin.Application.Common.Models;

public sealed record UpdateAdManagementSettingsResult(
    bool IsSuccess,
    string MessageKey,
    AdManagementSettingsModel? Settings,
    AdManagementValidationResult? Validation = null);
