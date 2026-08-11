using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using ITAdmin.Application.Common.Models;
using ITAdmin.Infrastructure.Services;

namespace ITAdmin.UnitTests.Services;

public sealed class LdapEndpointDiagnosticProbeTests
{
    [Fact]
    public void ResolveCertificateFailure_NameMismatch_ReturnsSafeSpecificMessageKey()
    {
        var result = LdapEndpointDiagnosticProbe.ResolveCertificateFailure(
            SslPolicyErrors.RemoteCertificateNameMismatch,
            X509ChainStatusFlags.NoError,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1));

        Assert.Equal(LdapConnectionDiagnosticMessageKeys.CertificateNameMismatch, result);
    }

    [Fact]
    public void ResolveCertificateFailure_Expired_ReturnsExpiredBeforeChainError()
    {
        var result = LdapEndpointDiagnosticProbe.ResolveCertificateFailure(
            SslPolicyErrors.RemoteCertificateChainErrors,
            X509ChainStatusFlags.NotTimeValid,
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow.AddDays(-1));

        Assert.Equal(LdapConnectionDiagnosticMessageKeys.CertificateExpired, result);
    }

    [Fact]
    public void ResolveCertificateFailure_UntrustedChain_ReturnsUntrusted()
    {
        var result = LdapEndpointDiagnosticProbe.ResolveCertificateFailure(
            SslPolicyErrors.RemoteCertificateChainErrors,
            X509ChainStatusFlags.UntrustedRoot,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1));

        Assert.Equal(LdapConnectionDiagnosticMessageKeys.CertificateUntrusted, result);
    }

    [Fact]
    public void ResolveCertificateFailure_NotYetValid_ReturnsNotYetValid()
    {
        var result = LdapEndpointDiagnosticProbe.ResolveCertificateFailure(
            SslPolicyErrors.RemoteCertificateChainErrors,
            X509ChainStatusFlags.NotTimeValid,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(30));

        Assert.Equal(LdapConnectionDiagnosticMessageKeys.CertificateNotYetValid, result);
    }

    [Fact]
    public void ResolveCertificateFailure_MissingCertificate_ReturnsInvalid()
    {
        var result = LdapEndpointDiagnosticProbe.ResolveCertificateFailure(
            SslPolicyErrors.RemoteCertificateNotAvailable,
            X509ChainStatusFlags.NoError,
            null,
            null);

        Assert.Equal(LdapConnectionDiagnosticMessageKeys.CertificateInvalid, result);
    }

    [Fact]
    public void ResolveCertificateFailure_HealthyCertificate_ReturnsNull() =>
        Assert.Null(LdapEndpointDiagnosticProbe.ResolveCertificateFailure(
            SslPolicyErrors.None,
            X509ChainStatusFlags.NoError,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1)));

    [Theory]
    [InlineData(X509ChainStatusFlags.RevocationStatusUnknown)]
    [InlineData(X509ChainStatusFlags.OfflineRevocation)]
    [InlineData(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)]
    public void IsOnlyRevocationIndeterminate_UnreachableRevocationAuthority_IsNotATrustFailure(
        X509ChainStatusFlags chainStatus) =>
        Assert.True(LdapEndpointDiagnosticProbe.IsOnlyRevocationIndeterminate(chainStatus));

    [Theory]
    [InlineData(X509ChainStatusFlags.NoError)]
    [InlineData(X509ChainStatusFlags.UntrustedRoot)]
    [InlineData(X509ChainStatusFlags.Revoked)]
    [InlineData(X509ChainStatusFlags.NotTimeValid)]
    [InlineData(X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.OfflineRevocation)]
    public void IsOnlyRevocationIndeterminate_RealChainDefects_StayFatal(X509ChainStatusFlags chainStatus) =>
        Assert.False(LdapEndpointDiagnosticProbe.IsOnlyRevocationIndeterminate(chainStatus));

    [Fact]
    public void DiagnosticMessageKeys_AreNamespacedAndCarryNoDirectoryInternals()
    {
        // Every stage message the API can emit must be a translation key, never raw exception text.
        var keys = typeof(LdapConnectionDiagnosticMessageKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.StartsWith("apiMessages.directoryDiagnostics.", key, StringComparison.Ordinal));
    }
}
