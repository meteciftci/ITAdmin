namespace ITAdmin.Api.Contracts.AdManagement;

public sealed record AdManagementNotificationRecipientSourceRequest
{
    public string Type { get; init; } = string.Empty;
    public string? Value { get; init; }
}

public sealed record AdManagementNotificationRuleRequest
{
    public Guid Id { get; init; }
    public string EventKey { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public AdManagementNotificationRecipientSourceRequest? RecipientSource { get; init; }
}

public sealed record AdManagementNotificationSettingsRequest
{
    public IReadOnlyList<AdManagementNotificationRuleRequest> Rules { get; init; } = [];
}

public sealed record AdManagementNotificationRecipientSourceResponse
{
    public string Type { get; init; } = string.Empty;
    public string? Value { get; init; }
}

public sealed record AdManagementNotificationRuleResponse
{
    public Guid Id { get; init; }
    public string EventKey { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public AdManagementNotificationRecipientSourceResponse? RecipientSource { get; init; }
}

public sealed record AdManagementNotificationSettingsResponse
{
    public IReadOnlyList<AdManagementNotificationRuleResponse> Rules { get; init; } = [];
}
