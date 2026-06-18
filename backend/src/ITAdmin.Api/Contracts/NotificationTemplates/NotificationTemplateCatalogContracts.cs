namespace ITAdmin.Api.Contracts.NotificationTemplates;

public sealed record NotificationTemplateCatalogResponse(
    IReadOnlyList<NotificationTemplateCatalogModuleResponse> Modules);

public sealed record NotificationTemplateCatalogModuleResponse(
    string Key,
    IReadOnlyList<NotificationTemplateCatalogEventResponse> Events);

public sealed record NotificationTemplateCatalogEventResponse(
    string Key,
    IReadOnlyList<string> SupportedChannels,
    IReadOnlyList<NotificationTemplateCatalogVariableResponse> Variables);

public sealed record NotificationTemplateCatalogVariableResponse(
    string Key,
    string? Example);
