using System.DirectoryServices.Protocols;
using ITAdmin.Application.Common.Models;
using ITAdmin.Infrastructure.Services;

namespace ITAdmin.UnitTests.Services;

/// <summary>
/// Ordered preferred-domain-controller failover behaviour. These lock in which failures move on to
/// the next controller and which stop immediately, because getting that wrong either takes the
/// module down when a single DC is out, or hammers every DC with rejected credentials.
/// </summary>
public sealed class AdDirectoryFailoverTests
{
    private const int AuthMethodNotSupported = 7;
    private const int StrongAuthRequired = 8;
    private const int InvalidCredentials = 49;
    private const int InsufficientAccessRights = 50;
    private const int Busy = 51;
    private const int UnwillingToPerform = 53;
    private const int ServerDown = 81;
    private const int LdapTimeout = 85;
    private const int ConnectError = 91;

    private static AdManagementConnectionParameters Connection(
        IReadOnlyList<string> preferredDomainControllers,
        string? domainFqdn = "example.local") =>
        new(
            domainFqdn,
            "EXAMPLE",
            "DC=example,DC=local",
            "DC=example,DC=local",
            null,
            null,
            null,
            null,
            preferredDomainControllers,
            "svc",
            "secret");

    [Fact]
    public void ResolveOrderedHosts_PreservesPreferredOrderAndAppendsDomainDiscoveryFallback() =>
        Assert.Equal(
            ["dc1.example.local", "dc2.example.local", "example.local"],
            AdDirectoryServiceBase.ResolveOrderedHosts(
                Connection(["dc1.example.local", "dc2.example.local", "DC1.example.local"])));

    [Fact]
    public void ResolveOrderedHosts_WithoutPreferredControllers_FallsBackToDomainFqdn() =>
        Assert.Equal(
            ["example.local"],
            AdDirectoryServiceBase.ResolveOrderedHosts(Connection([])));

    [Fact]
    public void ResolveOrderedHosts_DoesNotDuplicateDomainFqdnAlreadyListedAsPreferred() =>
        Assert.Equal(
            ["example.local", "dc2.example.local"],
            AdDirectoryServiceBase.ResolveOrderedHosts(
                Connection(["example.local", "dc2.example.local"])));

    [Fact]
    public void ResolveOrderedHosts_IsRecomputedPerCallSoRecoveredFirstControllerIsUsedAgain()
    {
        // No sticky "currently good DC" state exists: every operation restarts at the preferred
        // controller, which is what makes the first DC take over again once it recovers.
        var connection = Connection(["dc1.example.local", "dc2.example.local"]);

        Assert.Equal("dc1.example.local", AdDirectoryServiceBase.ResolveOrderedHosts(connection)[0]);
        Assert.Equal("dc1.example.local", AdDirectoryServiceBase.ResolveOrderedHosts(connection)[0]);
    }

    [Theory]
    [InlineData(ServerDown)]
    [InlineData(LdapTimeout)]
    [InlineData(ConnectError)]
    [InlineData(Busy)]
    [InlineData(UnwillingToPerform)]
    public void ShouldTryNextEndpoint_EndpointSpecificFailures_FailOver(int errorCode) =>
        Assert.True(AdDirectoryFailoverPolicy.ShouldTryNextEndpoint(new LdapException(errorCode)));

    [Theory]
    [InlineData(InvalidCredentials)]
    [InlineData(AuthMethodNotSupported)]
    [InlineData(StrongAuthRequired)]
    [InlineData(InsufficientAccessRights)]
    public void ShouldTryNextEndpoint_DomainWideFailures_DoNotFailOver(int errorCode) =>
        Assert.False(AdDirectoryFailoverPolicy.ShouldTryNextEndpoint(new LdapException(errorCode)));

    [Fact]
    public void ShouldTryNextEndpoint_NonLdapException_DoesNotFailOver() =>
        Assert.False(AdDirectoryFailoverPolicy.ShouldTryNextEndpoint(new InvalidOperationException()));

    [Fact]
    public void BindWithFailover_FirstControllerUnreachable_SecondControllerSucceeds()
    {
        var attempted = new List<string>();

        var connection = AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local"],
            host =>
            {
                attempted.Add(host);
                return new FakeConnection(host == "dc1.example.local" ? new LdapException(ServerDown) : null);
            },
            fake => fake.Bind());

        Assert.Equal(["dc1.example.local", "dc2.example.local"], attempted);
        Assert.False(connection.IsDisposed);
    }

    [Fact]
    public void BindWithFailover_CertificateRejectionOnFirstController_StillTriesSecondController()
    {
        // LDAPS certificates are per-DC, so a bad cert on dc1 must not take the whole domain down.
        var attempted = new List<string>();

        var connection = AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local"],
            host =>
            {
                attempted.Add(host);
                return new FakeConnection(host == "dc1.example.local" ? new LdapException(ConnectError) : null);
            },
            fake => fake.Bind());

        Assert.Equal(["dc1.example.local", "dc2.example.local"], attempted);
        Assert.False(connection.IsDisposed);
    }

    [Fact]
    public void BindWithFailover_AllControllersUnreachable_ThrowsAndDisposesEveryConnection()
    {
        var created = new List<FakeConnection>();

        var exception = Assert.Throws<LdapException>(() => AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local", "example.local"],
            _ =>
            {
                var connection = new FakeConnection(new LdapException(ServerDown));
                created.Add(connection);
                return connection;
            },
            fake => fake.Bind()));

        Assert.Equal(ServerDown, exception.ErrorCode);
        Assert.Equal(3, created.Count);
        Assert.All(created, connection => Assert.True(connection.IsDisposed));
    }

    [Fact]
    public void BindWithFailover_InvalidCredentials_StopsAtFirstControllerInsteadOfLoopingEveryDc()
    {
        // Retrying rejected credentials against every DC only multiplies account-lockout risk.
        var attempted = new List<string>();
        var created = new List<FakeConnection>();

        var exception = Assert.Throws<LdapException>(() => AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local"],
            host =>
            {
                attempted.Add(host);
                var connection = new FakeConnection(new LdapException(InvalidCredentials));
                created.Add(connection);
                return connection;
            },
            fake => fake.Bind()));

        Assert.Equal(InvalidCredentials, exception.ErrorCode);
        Assert.Equal(["dc1.example.local"], attempted);
        Assert.All(created, connection => Assert.True(connection.IsDisposed));
    }

    [Fact]
    public void BindWithFailover_NonLdapFailure_PropagatesImmediatelyWithoutFailingOver()
    {
        var attempted = new List<string>();

        Assert.Throws<InvalidOperationException>(() => AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local"],
            host =>
            {
                attempted.Add(host);
                return new FakeConnection(new InvalidOperationException());
            },
            fake => fake.Bind()));

        Assert.Equal(["dc1.example.local"], attempted);
    }

    [Fact]
    public void BindWithFailover_CancellationRequested_StopsBeforeContactingAnyController()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local"],
            _ => throw new InvalidOperationException("Should not be reached."),
            (FakeConnection fake) => fake.Bind(),
            cancellation.Token));
    }

    [Fact]
    public void BindWithFailover_CancellationBetweenControllers_StopsWithoutTryingTheRest()
    {
        using var cancellation = new CancellationTokenSource();
        var attempted = new List<string>();

        Assert.Throws<OperationCanceledException>(() => AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local"],
            host =>
            {
                attempted.Add(host);
                cancellation.Cancel();
                return new FakeConnection(new LdapException(ServerDown));
            },
            fake => fake.Bind(),
            cancellation.Token));

        Assert.Equal(["dc1.example.local"], attempted);
    }

    [Fact]
    public void ShouldTryNextEndpoint_Cancellation_DoesNotFailOver() =>
        // A cancelled request is not an endpoint fault, so there is nothing to fail over to.
        Assert.False(AdDirectoryFailoverPolicy.ShouldTryNextEndpoint(new OperationCanceledException()));

    [Fact]
    public void BindWithFailover_CancellationRaisedByTheBindItself_PropagatesAndSkipsRemainingControllers()
    {
        // The token can trip while the first bind is already in flight; that must surface as
        // cancellation rather than being retried against dc2 or reported as an LDAP failure.
        using var cancellation = new CancellationTokenSource();
        var attempted = new List<string>();
        var created = new List<FakeConnection>();

        Assert.Throws<OperationCanceledException>(() => AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local"],
            host =>
            {
                attempted.Add(host);
                var connection = new FakeConnection(new OperationCanceledException(cancellation.Token));
                created.Add(connection);
                return connection;
            },
            fake => fake.Bind(),
            cancellation.Token));

        Assert.Equal(["dc1.example.local"], attempted);
        // Cancellation must not leak the half-open connection.
        Assert.All(created, connection => Assert.True(connection.IsDisposed));
    }

    [Fact]
    public void BindWithFailover_CancellationIsNeverRewrittenAsAnLdapFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = Record.Exception(() => AdDirectoryFailoverPolicy.BindWithFailover(
            ["dc1.example.local", "dc2.example.local"],
            _ => new FakeConnection(null),
            fake => fake.Bind(),
            cancellation.Token));

        Assert.IsNotType<LdapException>(exception);
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    public void BindWithFailover_NoHostsConfigured_ThrowsServerDownRatherThanNullReference()
    {
        var exception = Assert.Throws<LdapException>(() => AdDirectoryFailoverPolicy.BindWithFailover(
            [],
            _ => throw new InvalidOperationException("Should not be reached."),
            (FakeConnection fake) => fake.Bind()));

        Assert.Equal(ServerDown, exception.ErrorCode);
    }

    [Fact]
    public void SelectMostMeaningfulFailure_PrefersDirectoryAnswerOverUnreachableHost()
    {
        var answered = new LdapException(UnwillingToPerform);

        Assert.Same(
            answered,
            AdDirectoryFailoverPolicy.SelectMostMeaningfulFailure(
                [new LdapException(ServerDown), answered, new LdapException(LdapTimeout)]));
    }

    [Fact]
    public void SelectMostMeaningfulFailure_EquallyRankedFailures_KeepsTheMostPreferredController()
    {
        var first = new LdapException(ServerDown);

        Assert.Same(
            first,
            AdDirectoryFailoverPolicy.SelectMostMeaningfulFailure(
                [first, new LdapException(LdapTimeout), new LdapException(ConnectError)]));
    }

    /// <summary>
    /// Connection handle stand-in that either throws a supplied bind failure or succeeds, and
    /// records whether the failover loop disposed it.
    /// </summary>
    private sealed class FakeConnection(Exception? failure) : IDisposable
    {
        internal bool IsDisposed { get; private set; }

        internal void Bind()
        {
            if (failure is not null)
            {
                throw failure;
            }
        }

        public void Dispose() => IsDisposed = true;
    }
}
