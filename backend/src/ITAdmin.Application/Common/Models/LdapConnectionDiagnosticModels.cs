namespace ITAdmin.Application.Common.Models;

public static class LdapConnectionDiagnosticStatuses
{
    public const string Ok = "Ok";
    public const string Warning = "Warning";
    public const string Failed = "Failed";
}

public static class LdapConnectionDiagnosticMessageKeys
{
    private const string Prefix = "apiMessages.directoryDiagnostics.";

    public const string DnsResolved = Prefix + "dnsResolved";
    public const string DnsResolutionFailed = Prefix + "dnsResolutionFailed";
    public const string TcpConnected = Prefix + "tcpConnected";
    public const string TcpConnectionFailed = Prefix + "tcpConnectionFailed";
    public const string TlsSucceeded = Prefix + "tlsSucceeded";
    public const string TlsHandshakeFailed = Prefix + "tlsHandshakeFailed";
    public const string CertificateTrusted = Prefix + "certificateTrusted";
    public const string CertificateUntrusted = Prefix + "certificateUntrusted";
    public const string CertificateNameMismatch = Prefix + "certificateNameMismatch";
    public const string CertificateExpired = Prefix + "certificateExpired";
    public const string CertificateNotYetValid = Prefix + "certificateNotYetValid";
    public const string CertificateInvalid = Prefix + "certificateInvalid";
    public const string CertificateRevocationUnknown = Prefix + "certificateRevocationUnknown";
    public const string BindSucceeded = Prefix + "bindSucceeded";
    public const string BindCredentialsRejected = Prefix + "bindCredentialsRejected";
    public const string BindFailed = Prefix + "bindFailed";
    public const string BaseDnResolved = Prefix + "baseDnResolved";
    public const string BaseDnNotResolved = Prefix + "baseDnNotResolved";
    public const string UserSearchBaseResolved = Prefix + "userSearchBaseResolved";
    public const string UserSearchBaseNotResolved = Prefix + "userSearchBaseNotResolved";
    public const string DirectoryContextResolved = Prefix + "directoryContextResolved";
    public const string UserSearchSucceeded = Prefix + "userSearchSucceeded";
    public const string UserSearchFailed = Prefix + "userSearchFailed";
    public const string TestUserBindSucceeded = Prefix + "testUserBindSucceeded";
    public const string TestUserBindFailed = Prefix + "testUserBindFailed";
}

public sealed record LdapConnectionDiagnosticDetail(
    string Key,
    string Status,
    string MessageKey,
    IReadOnlyDictionary<string, object>? MessageParams = null);

public sealed record LdapConnectionDiagnosticResult(
    bool IsValid,
    string Host,
    IReadOnlyList<LdapConnectionDiagnosticDetail> Details);

public sealed record LdapConnectionDiagnosticRequest(
    string Host,
    string BindUserName,
    string? BindUserDomain,
    string BindPassword);
