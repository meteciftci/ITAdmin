namespace ITAdmin.Api.Contracts.SystemHttps;

public sealed record SystemHttpsStatusResponse(
    bool AgentAvailable,
    bool Enabled,
    int Port,
    bool RedirectHttpToHttps,
    string? CertificateThumbprint,
    string? CertificateSubject,
    DateTimeOffset? CertificateNotAfterUtc,
    string Message);

public sealed record ConfigureSystemHttpsResponse(
    bool Enabled,
    int Port,
    bool RedirectHttpToHttps,
    string? CertificateThumbprint,
    string? CertificateSubject,
    DateTimeOffset? CertificateNotAfterUtc,
    string Message);
