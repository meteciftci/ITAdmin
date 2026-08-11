namespace ITAdmin.Api.Contracts.Settings;

public sealed record LdapConnectionDiagnosticDetailResponse(
    string Key,
    string Status,
    string MessageKey,
    IReadOnlyDictionary<string, object>? MessageParams = null);
