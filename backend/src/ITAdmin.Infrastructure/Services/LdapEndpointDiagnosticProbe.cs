using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;

namespace ITAdmin.Infrastructure.Services;

internal static class LdapEndpointDiagnosticProbe
{
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(8);

    internal static async Task<LdapConnectionDiagnosticResult> RunAsync(
        LdapConnectionDiagnosticRequest request,
        CancellationToken cancellationToken)
    {
        var host = request.Host.Trim();
        var details = new List<LdapConnectionDiagnosticDetail>();
        var target = new Dictionary<string, object> { ["host"] = host };

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken)
                .WaitAsync(StepTimeout, cancellationToken);
            if (addresses.Length == 0)
            {
                details.Add(Failed("dns", LdapConnectionDiagnosticMessageKeys.DnsResolutionFailed, target));
                return new(false, host, details);
            }

            details.Add(Ok("dns", LdapConnectionDiagnosticMessageKeys.DnsResolved, target));
        }
        catch (Exception exception) when (exception is SocketException or TimeoutException or OperationCanceledException)
        {
            details.Add(Failed("dns", LdapConnectionDiagnosticMessageKeys.DnsResolutionFailed, target));
            return new(false, host, details);
        }

        using var tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync(host, LdapConnectionDefaults.StandardLdapsPort, cancellationToken)
                .AsTask()
                .WaitAsync(StepTimeout, cancellationToken);
            details.Add(Ok("tcp", LdapConnectionDiagnosticMessageKeys.TcpConnected, target));
        }
        catch (Exception exception) when (exception is SocketException or TimeoutException or OperationCanceledException)
        {
            details.Add(Failed("tcp", LdapConnectionDiagnosticMessageKeys.TcpConnectionFailed, target));
            return new(false, host, details);
        }

        SslPolicyErrors certificateErrors = SslPolicyErrors.None;
        X509ChainStatusFlags chainStatus = X509ChainStatusFlags.NoError;
        DateTime? certificateNotBefore = null;
        DateTime? certificateNotAfter = null;
        var revocationIndeterminate = false;
        using (var sslStream = new SslStream(
                   tcpClient.GetStream(),
                   leaveInnerStreamOpen: false,
                   (_, certificate, chain, errors) =>
                   {
                       certificateErrors = errors;
                       if (certificate is not null)
                       {
                           var certificate2 = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                           certificateNotBefore = certificate2.NotBefore.ToUniversalTime();
                           certificateNotAfter = certificate2.NotAfter.ToUniversalTime();
                       }

                       if (chain is not null)
                       {
                           foreach (var status in chain.ChainStatus)
                           {
                               chainStatus |= status.Status;
                           }
                       }

                       if (errors == SslPolicyErrors.None)
                       {
                           return true;
                       }

                       // An unreachable CRL/OCSP responder is not a trust failure: Schannel accepts it
                       // for LDAPS by default, so failing the probe here would report a healthy DC as
                       // broken. Everything else (untrusted root, name mismatch, validity) stays fatal.
                       if (errors == SslPolicyErrors.RemoteCertificateChainErrors
                           && IsOnlyRevocationIndeterminate(chainStatus))
                       {
                           revocationIndeterminate = true;
                           return true;
                       }

                       return false;
                   }))
        {
            try
            {
                await sslStream.AuthenticateAsClientAsync(
                        new SslClientAuthenticationOptions
                        {
                            TargetHost = host,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                            CertificateRevocationCheckMode = X509RevocationMode.Online,
                        },
                        cancellationToken)
                    .WaitAsync(StepTimeout, cancellationToken);
            }
            catch (Exception exception) when (exception is AuthenticationException or IOException or TimeoutException or OperationCanceledException)
            {
                var certificateMessage = ResolveCertificateFailure(
                    certificateErrors,
                    chainStatus,
                    certificateNotBefore,
                    certificateNotAfter);
                details.Add(Failed(
                    certificateMessage is null ? "tls" : "certificate",
                    certificateMessage ?? LdapConnectionDiagnosticMessageKeys.TlsHandshakeFailed,
                    target));
                return new(false, host, details);
            }
        }

        details.Add(Ok("tls", LdapConnectionDiagnosticMessageKeys.TlsSucceeded, target));
        details.Add(revocationIndeterminate
            ? Warning("certificate", LdapConnectionDiagnosticMessageKeys.CertificateRevocationUnknown, target)
            : Ok("certificate", LdapConnectionDiagnosticMessageKeys.CertificateTrusted, target));

        var bindIdentity = LdapService.BuildBindIdentityForDiagnostics(
            request.BindUserName,
            request.BindUserDomain);
        var identifier = new LdapDirectoryIdentifier(host, LdapConnectionDefaults.StandardLdapsPort);
        using var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Credential = new NetworkCredential(bindIdentity, request.BindPassword),
            Timeout = StepTimeout,
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = true;

        try
        {
            connection.Bind();
            details.Add(Ok("bind", LdapConnectionDiagnosticMessageKeys.BindSucceeded, target));
            return new(true, host, details);
        }
        catch (LdapException exception)
        {
            var messageKey = LdapBindFailureMessageResolver.IsCredentialFailure(exception)
                ? LdapConnectionDiagnosticMessageKeys.BindCredentialsRejected
                : LdapConnectionDiagnosticMessageKeys.BindFailed;
            details.Add(Failed("bind", messageKey, target));
            return new(false, host, details);
        }
        catch (Exception)
        {
            details.Add(Failed("bind", LdapConnectionDiagnosticMessageKeys.BindFailed, target));
            return new(false, host, details);
        }
    }

    /// <summary>
    /// True when every reported chain problem is "we could not reach the revocation authority",
    /// as opposed to an actual trust, naming, or validity defect.
    /// </summary>
    internal static bool IsOnlyRevocationIndeterminate(X509ChainStatusFlags chainStatus)
    {
        const X509ChainStatusFlags RevocationIndeterminate =
            X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;

        return chainStatus != X509ChainStatusFlags.NoError
            && (chainStatus & ~RevocationIndeterminate) == X509ChainStatusFlags.NoError;
    }

    internal static string? ResolveCertificateFailure(
        SslPolicyErrors errors,
        X509ChainStatusFlags chainStatus,
        DateTime? notBefore,
        DateTime? notAfter)
    {
        var now = DateTime.UtcNow;
        if (notBefore.HasValue && now < notBefore.Value)
        {
            return LdapConnectionDiagnosticMessageKeys.CertificateNotYetValid;
        }

        if (notAfter.HasValue && now > notAfter.Value)
        {
            return LdapConnectionDiagnosticMessageKeys.CertificateExpired;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
        {
            return LdapConnectionDiagnosticMessageKeys.CertificateNameMismatch;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0
            || chainStatus != X509ChainStatusFlags.NoError)
        {
            return LdapConnectionDiagnosticMessageKeys.CertificateUntrusted;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
        {
            return LdapConnectionDiagnosticMessageKeys.CertificateInvalid;
        }

        return null;
    }

    private static LdapConnectionDiagnosticDetail Ok(
        string key,
        string messageKey,
        IReadOnlyDictionary<string, object> messageParams) =>
        new(key, LdapConnectionDiagnosticStatuses.Ok, messageKey, messageParams);

    private static LdapConnectionDiagnosticDetail Warning(
        string key,
        string messageKey,
        IReadOnlyDictionary<string, object> messageParams) =>
        new(key, LdapConnectionDiagnosticStatuses.Warning, messageKey, messageParams);

    private static LdapConnectionDiagnosticDetail Failed(
        string key,
        string messageKey,
        IReadOnlyDictionary<string, object> messageParams) =>
        new(key, LdapConnectionDiagnosticStatuses.Failed, messageKey, messageParams);
}
