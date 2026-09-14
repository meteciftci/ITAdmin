using ITAdmin.HostAgent;
using ITAdmin.HostAgent.Contracts;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The privilege boundary between the ITAdmin web application and the privileged host agent.
///
/// <para>
/// These are security tests, not feature tests. The boundary is the reason the web application can
/// keep an unprivileged app pool identity while ITAdmin still updates itself, so its properties are
/// asserted directly rather than inferred from how the code happens to be wired today.
/// </para>
/// </summary>
public sealed class HostAgentBoundaryTests
{
    private const string AppPool = "ITAdmin";
    private static readonly HostAgentAuthorization Authorization = new(AppPool);

    private static HostAgentCallerContext WebApplication() => new(@"IIS APPPOOL\ITAdmin", false);
    private static HostAgentCallerContext Administrator() => new(@"CORP\admin", true);
    private static HostAgentCallerContext Unknown() => new(@"CORP\someone", false);

    // ------------------------------------------------------------------------------------------
    // The contract itself
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Protocol_ExposesNoGenericExecutionOperation()
    {
        // The single most important property of this contract. If an operation ever appears that
        // takes a command, a script path, or a shell string, the boundary is gone: a flaw in
        // request handling would become arbitrary code execution as LocalSystem.
        foreach (var operation in Enum.GetNames<HostAgentOperation>())
        {
            foreach (var forbidden in new[] { "Execute", "Run", "Invoke", "Script", "Command", "Shell", "Powershell" })
            {
                Assert.False(
                    operation.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"Host agent operation '{operation}' looks like generic execution.");
            }
        }
    }

    [Fact]
    public void Protocol_RequestCarriesNoPathOrCommandFields()
    {
        // The certificate bytes / password / port for ConfigureHttps are validated data the agent
        // acts on directly - not a path, a command, a script name, or a git ref a caller could use
        // to steer a privileged operation.
        foreach (var property in typeof(HostAgentRequest).GetProperties())
        {
            foreach (var forbidden in new[] { "path", "command", "script", "argument", "executable", "directory", "branch", "commit", "targetversion", "repositoryurl" })
            {
                Assert.False(
                    property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"HostAgentRequest.{property.Name} would let a caller steer a privileged operation.");
            }
        }
    }

    [Fact]
    public void Protocol_ConfigureHttpsValidatesItsPayload()
    {
        var goodPfx = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        Assert.Empty(new HostAgentRequest
        {
            Operation = HostAgentOperation.ConfigureHttps,
            PfxBase64 = goodPfx,
            PfxPassword = "",
            HttpsPort = 443,
        }.Validate());

        Assert.NotEmpty(new HostAgentRequest
        {
            Operation = HostAgentOperation.ConfigureHttps,
            PfxBase64 = "not base64!!",
            PfxPassword = "",
        }.Validate());

        Assert.NotEmpty(new HostAgentRequest
        {
            Operation = HostAgentOperation.ConfigureHttps,
            PfxBase64 = goodPfx,
            PfxPassword = null,
        }.Validate());

        Assert.NotEmpty(new HostAgentRequest
        {
            Operation = HostAgentOperation.ConfigureHttps,
            PfxBase64 = goodPfx,
            PfxPassword = "",
            HttpsPort = 70000,
        }.Validate());
    }

    [Fact]
    public void Protocol_ResponseCarriesNoSecretOrInternalPathFields()
    {
        // Responses are rendered in the ITAdmin UI.
        foreach (var type in new[]
                 {
                     typeof(HostAgentResponse), typeof(HostAgentInstallationStatus),
                     typeof(HostAgentUpdateStatus), typeof(HostAgentUpdateAvailability),
                     typeof(HostAgentHttpsStatus), typeof(HostAgentDnsProbeResult),
                     typeof(HostAgentDnsCapabilities), typeof(HostAgentDnsInventoryPage),
                     typeof(HostAgentDnsZoneInventoryItem), typeof(HostAgentDnsRecordInventoryItem),
                     typeof(HostAgentDnsRecordMutationResult), typeof(HostAgentDnsZoneMutationResult),
                 })
        {
            foreach (var property in type.GetProperties())
            {
                foreach (var forbidden in new[] { "secret", "password", "token", "repositoryUrl", "privateKey", "pfx", "path" })
                {
                    Assert.False(
                        property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                        $"{type.Name}.{property.Name} would leak deployment-authority detail to the web UI.");
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // Authorization
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(HostAgentOperation.Ping)]
    [InlineData(HostAgentOperation.GetInstallationStatus)]
    [InlineData(HostAgentOperation.CheckForUpdates)]
    [InlineData(HostAgentOperation.RequestUpdate)]
    [InlineData(HostAgentOperation.GetUpdateStatus)]
    [InlineData(HostAgentOperation.RecycleApplicationPool)]
    [InlineData(HostAgentOperation.GetHttpsStatus)]
    [InlineData(HostAgentOperation.ConfigureHttps)]
    [InlineData(HostAgentOperation.DisableHttps)]
    [InlineData(HostAgentOperation.TestDnsServerConnection)]
    [InlineData(HostAgentOperation.ReadDnsServerInventoryPage)]
    [InlineData(HostAgentOperation.MutateDnsServerResourceRecord)]
    [InlineData(HostAgentOperation.MutateDnsServerZone)]
    [InlineData(HostAgentOperation.ManageDnsServerSettings)]
    [InlineData(HostAgentOperation.ManageDnsPolicyConfiguration)]
    [InlineData(HostAgentOperation.ManageDnssecConfiguration)]
    [InlineData(HostAgentOperation.ManageDnsScavenging)]
    [InlineData(HostAgentOperation.ManageDnsNetworkConfiguration)]
    [InlineData(HostAgentOperation.ManageDnsZoneTransfers)]
    public void Authorization_WebApplicationMayInvokeTheUpdateAndSettingsOperations(HostAgentOperation operation) =>
        Assert.True(Authorization.Authorize(@"IIS APPPOOL\ITAdmin", false, operation).IsAllowed);

    [Fact]
    public void Authorization_UnknownCallerIsDenied()
    {
        // Reaching this code as an unrecognised principal means the pipe ACL was circumvented or
        // misconfigured; "I do not know who you are" on a privileged channel is a no.
        var decision = Authorization.Authorize(@"CORP\someone", false, HostAgentOperation.Ping);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Authorization_UnidentifiedCallerIsDenied() =>
        Assert.False(Authorization.Authorize(null, false, HostAgentOperation.Ping).IsAllowed);

    [Fact]
    public void Authorization_DifferentAppPoolIsDenied()
    {
        // Another application on the same server, running under its own pool, is not ITAdmin.
        Assert.False(Authorization.Authorize(@"IIS APPPOOL\SomethingElse", false, HostAgentOperation.RequestUpdate).IsAllowed);
    }

    [Fact]
    public void Authorization_AppPoolIdentityIsDerivedFromTheConfiguredPoolName() =>
        Assert.Equal(@"IIS APPPOOL\Contoso-ITAdmin", new HostAgentAuthorization("Contoso-ITAdmin").AppPoolIdentity);

    [Fact]
    public void Authorization_MatchesTheAppPoolBySidEvenWhenTheImpersonatedNameDoesNot()
    {
        // An impersonated token's name can come back as something other than the literal
        // "IIS APPPOOL\<name>"; its SID is exact. When the agent resolved the pool SID at start-up,
        // a SID match authorizes the caller regardless of the name string.
        const string poolSid = "S-1-5-82-1234567890-123456789-1234567890-123456789-1234567890";
        var authorization = new HostAgentAuthorization("ITAdmin", poolSid);

        Assert.True(authorization
            .Authorize("IIS APPPOOL\\ITAdmin (rendered oddly)", false, HostAgentOperation.RequestUpdate, poolSid)
            .IsAllowed);
    }

    [Fact]
    public void Authorization_RejectsAForeignSidEvenWithAMatchingLooseName()
    {
        const string poolSid = "S-1-5-82-1111111111-111111111-1111111111-111111111-1111111111";
        var authorization = new HostAgentAuthorization("ITAdmin", poolSid);

        // Wrong SID and a non-matching name: denied.
        Assert.False(authorization
            .Authorize("IIS APPPOOL\\SomethingElse", false, HostAgentOperation.RequestUpdate, "S-1-5-82-9")
            .IsAllowed);

        // The name fallback still works for hosts where no SID was resolved.
        Assert.True(new HostAgentAuthorization("ITAdmin")
            .Authorize("IIS APPPOOL\\ITAdmin", false, HostAgentOperation.RequestUpdate, "S-1-5-82-9")
            .IsAllowed);
    }

    [Fact]
    public void Authorization_AdministratorMayInvokeEverything()
    {
        foreach (var operation in Enum.GetValues<HostAgentOperation>())
        {
            Assert.True(Authorization.Authorize(@"CORP\admin", true, operation).IsAllowed);
        }
    }

    [Fact]
    public void Authorization_UndefinedOperationIsDenied() =>
        Assert.False(Authorization.Authorize(@"CORP\admin", true, (HostAgentOperation)9999).IsAllowed);

    // ------------------------------------------------------------------------------------------
    // Dispatch
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Dispatch_UnparseableRequestIsRejectedBeforeAnythingElse()
    {
        var operations = new RecordingOperations();
        var dispatcher = new HostAgentDispatcher(Authorization, operations);

        var response = await dispatcher.DispatchAsync("{ not json", Administrator());

        Assert.Equal(HostAgentResponseStatus.Rejected, response.Status);
        Assert.Empty(operations.Invoked);
    }

    [Fact]
    public async Task Dispatch_ShapeIsValidatedBeforeAuthorization()
    {
        // Ordering matters: an unauthorized caller must not be able to learn anything from the
        // difference between "rejected" and "denied".
        var operations = new RecordingOperations();
        var dispatcher = new HostAgentDispatcher(Authorization, operations);

        var response = await dispatcher.DispatchAsync(
            new HostAgentRequest { ProtocolVersion = 99, Operation = HostAgentOperation.RequestUpdate }.ToJson(),
            Unknown());

        Assert.Equal(HostAgentResponseStatus.Rejected, response.Status);
        Assert.Empty(operations.Invoked);
    }

    [Fact]
    public async Task Dispatch_DeniedCallerNeverReachesAnOperation()
    {
        var operations = new RecordingOperations();
        var dispatcher = new HostAgentDispatcher(Authorization, operations);

        var response = await dispatcher.DispatchAsync(
            new HostAgentRequest { Operation = HostAgentOperation.RequestUpdate }.ToJson(),
            Unknown());

        Assert.Equal(HostAgentResponseStatus.Denied, response.Status);
        Assert.Empty(operations.Invoked);
    }

    [Fact]
    public async Task Dispatch_ProtocolVersionMismatchIsRejected()
    {
        var dispatcher = new HostAgentDispatcher(Authorization, new RecordingOperations());

        var response = await dispatcher.DispatchAsync(
            new HostAgentRequest { ProtocolVersion = 99, Operation = HostAgentOperation.Ping }.ToJson(),
            Administrator());

        Assert.Equal(HostAgentResponseStatus.Rejected, response.Status);
    }

    [Fact]
    public async Task Dispatch_OperationFailureIsReportedWithoutLeakingInternals()
    {
        var operations = new ThrowingOperations(new InvalidOperationException(
            @"fatal: could not read C:\ITAdmin\src\.git\config"));
        var dispatcher = new HostAgentDispatcher(Authorization, operations);

        var response = await dispatcher.DispatchAsync(
            new HostAgentRequest { Operation = HostAgentOperation.CheckForUpdates }.ToJson(),
            WebApplication());

        Assert.Equal(HostAgentResponseStatus.Failed, response.Status);
        Assert.DoesNotContain(@"C:\ITAdmin", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(operations.Logged);
    }

    [Fact]
    public async Task Dispatch_CorrelationIdIsEchoed()
    {
        var dispatcher = new HostAgentDispatcher(Authorization, new RecordingOperations());

        var response = await dispatcher.DispatchAsync(
            new HostAgentRequest { Operation = HostAgentOperation.Ping, CorrelationId = "abc-123" }.ToJson(),
            Administrator());

        Assert.Equal("abc-123", response.CorrelationId);
    }

    [Fact]
    public void Protocol_DnsProbeRequiresACompleteTypedConnectionPayload()
    {
        var valid = new HostAgentRequest
        {
            Operation = HostAgentOperation.TestDnsServerConnection,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
        };
        Assert.Empty(valid.Validate());
        Assert.NotEmpty((valid with { DnsHostName = "https://invalid/path" }).Validate());
        Assert.NotEmpty((valid with { DnsPort = 0 }).Validate());
        Assert.NotEmpty((valid with { DnsTlsCertificateThumbprint = "not-a-thumbprint" }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnsProbeUsesOnlyTheRegisteredTypedExecutor()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(
            Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.TestDnsServerConnection,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.CallCount);
        Assert.True(response.DnsProbe!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnsProbeScript_IsFixedAndDoesNotInterpolateRequestValues()
    {
        Assert.DoesNotContain("DnsHostName", DnsRemoteCapabilityProbe.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("DnsUserName", DnsRemoteCapabilityProbe.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("$(`", DnsRemoteCapabilityProbe.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Protocol_DnsInventoryRequiresBoundedTypedPaging()
    {
        var valid = new HostAgentRequest
        {
            Operation = HostAgentOperation.ReadDnsServerInventoryPage,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
            DnsInventoryKind = HostAgentDnsInventoryKind.Records,
            DnsZoneName = "example.local",
            DnsInventoryOffset = 0,
            DnsInventoryPageSize = 250,
        };

        Assert.Empty(valid.Validate());
        Assert.NotEmpty((valid with { DnsZoneName = null }).Validate());
        Assert.NotEmpty((valid with { DnsInventoryPageSize = 501 }).Validate());
        Assert.Empty((valid with { DnsInventoryKind = HostAgentDnsInventoryKind.Zones, DnsZoneName = null }).Validate());
    }

    [Fact]
    public void DnsInventoryScript_IsFixedAndUsesBoundParameters()
    {
        Assert.StartsWith("param(", DnsRemoteInventoryProbe.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("DnsPassword", DnsRemoteInventoryProbe.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemoteInventoryProbe.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ValidateRange(1,500)", DnsRemoteInventoryProbe.Script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatch_DnsInventoryUsesTypedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(
            Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.ReadDnsServerInventoryPage,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
            DnsInventoryKind = HostAgentDnsInventoryKind.Zones,
            DnsInventoryOffset = 0,
            DnsInventoryPageSize = 250,
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.InventoryCallCount);
        Assert.True(response.DnsInventoryPage!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Protocol_DnsRecordMutationRequiresTypedBoundedDataAndConcurrencyHash()
    {
        var valid = new HostAgentRequest
        {
            Operation = HostAgentOperation.MutateDnsServerResourceRecord,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
            DnsRecordMutationKind = HostAgentDnsRecordMutationKind.Update,
            DnsZoneName = "example.local",
            DnsRecordRelativeName = "www",
            DnsRecordType = "A",
            DnsRecordValues = ["10.0.0.20"],
            DnsRecordTimeToLiveSeconds = 300,
            DnsExpectedRecordHash = new string('a', 64),
            DnsExpectedRecordDataJson = "{\"IPv4Address\":\"10.0.0.10\"}",
            DnsExpectedRecordTimeToLiveSeconds = 300,
        };

        Assert.Empty(valid.Validate());
        Assert.NotEmpty((valid with { DnsRecordType = "SOA" }).Validate());
        Assert.NotEmpty((valid with { DnsExpectedRecordHash = null }).Validate());
        Assert.NotEmpty((valid with { DnsRecordValues = [new string('x', 2049)] }).Validate());
        Assert.Empty((valid with
        {
            DnsRecordMutationKind = HostAgentDnsRecordMutationKind.Create,
            DnsExpectedRecordHash = null,
        }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnsRecordMutationUsesFixedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(
            Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.MutateDnsServerResourceRecord,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
            DnsRecordMutationKind = HostAgentDnsRecordMutationKind.Delete,
            DnsZoneName = "example.local",
            DnsRecordRelativeName = "www",
            DnsRecordType = "A",
            DnsRecordTimeToLiveSeconds = 300,
            DnsExpectedRecordHash = new string('a', 64),
            DnsExpectedRecordDataJson = "{\"IPv4Address\":\"10.0.0.10\"}",
            DnsExpectedRecordTimeToLiveSeconds = 300,
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.MutationCallCount);
        Assert.True(response.DnsRecordMutation!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnsRecordMutationScript_IsFixedAndUsesBoundParameters()
    {
        Assert.StartsWith("param(", DnsRemoteRecordMutation.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("DnsPassword", DnsRemoteRecordMutation.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemoteRecordMutation.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExpectedRecordDataJson", DnsRemoteRecordMutation.Script, StringComparison.Ordinal);
        System.Management.Automation.Language.Parser.ParseInput(
            DnsRemoteRecordMutation.Script, out _, out var parseErrors);
        Assert.Empty(parseErrors);
    }

    [Fact]
    public void Protocol_DnsZoneMutationRequiresTypedBoundedConfigurationAndExpectedState()
    {
        var valid = new HostAgentRequest
        {
            Operation = HostAgentOperation.MutateDnsServerZone,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
            DnsZoneMutationKind = HostAgentDnsZoneMutationKind.Update,
            DnsZoneKind = HostAgentDnsZoneKind.Forwarder,
            DnsZoneName = "partners.example",
            DnsZoneIsDsIntegrated = false,
            DnsZoneMasterServers = ["192.0.2.10"],
            DnsZoneForwarderTimeoutSeconds = 5,
            DnsZoneUseRecursion = false,
            DnsExpectedZoneStateJson = "{\"Name\":\"partners.example\"}",
        };

        Assert.Empty(valid.Validate());
        Assert.NotEmpty((valid with { DnsExpectedZoneStateJson = null }).Validate());
        Assert.NotEmpty((valid with { DnsZoneMasterServers = ["not-an-ip"] }).Validate());
        Assert.NotEmpty((valid with { DnsVirtualizationInstance = "tenant" }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnsZoneMutationUsesFixedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(
            Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.MutateDnsServerZone,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
            DnsZoneMutationKind = HostAgentDnsZoneMutationKind.Create,
            DnsZoneKind = HostAgentDnsZoneKind.Primary,
            DnsZoneName = "example.local",
            DnsZoneIsDsIntegrated = false,
            DnsZoneDynamicUpdate = "None",
            DnsZoneFile = "example.local.dns",
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.ZoneMutationCallCount);
        Assert.True(response.DnsZoneMutation!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnsZoneMutationScript_IsFixedAndUsesBoundParameters()
    {
        Assert.StartsWith("param(", DnsRemoteZoneMutation.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("DnsPassword", DnsRemoteZoneMutation.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemoteZoneMutation.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExpectedZoneStateJson", DnsRemoteZoneMutation.Script, StringComparison.Ordinal);
        System.Management.Automation.Language.Parser.ParseInput(
            DnsRemoteZoneMutation.Script, out _, out var parseErrors);
        Assert.Empty(parseErrors);
    }

    [Fact]
    public void Protocol_DnsServerSettingsUpdateRequiresTypedBoundedConfigurationAndExpectedState()
    {
        var valid = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsServerSettings,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
            DnsServerSettingsAction = HostAgentDnsServerSettingsAction.Update,
            DnsForwarderAddresses = ["192.0.2.10"],
            DnsForwarderUseRootHint = false,
            DnsForwarderTimeoutSeconds = 5,
            DnsForwarderEnableReordering = true,
            DnsRecursionEnabled = true,
            DnsRecursionAdditionalTimeoutSeconds = 4,
            DnsRecursionRetryIntervalSeconds = 3,
            DnsRecursionTimeoutSeconds = 8,
            DnsRecursionSecureResponse = true,
            DnsExpectedServerSettingsJson = "{}",
        };

        Assert.Empty(valid.Validate());
        Assert.NotEmpty((valid with { DnsExpectedServerSettingsJson = null }).Validate());
        Assert.NotEmpty((valid with { DnsForwarderAddresses = ["not-an-ip"] }).Validate());
        Assert.NotEmpty((valid with { DnsRecursionRetryIntervalSeconds = 16 }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnsServerSettingsUsesFixedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(
            Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsServerSettings,
            DnsHostName = "dns01.example.local",
            DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc",
            DnsPassword = "secret",
            DnsTimeoutSeconds = 30,
            DnsServerSettingsAction = HostAgentDnsServerSettingsAction.ClearCache,
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.ServerSettingsCallCount);
        Assert.True(response.DnsServerSettings!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnsServerSettingsScript_IsFixedAndUsesBoundParameters()
    {
        Assert.StartsWith("param(", DnsRemoteServerSettings.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("DnsPassword", DnsRemoteServerSettings.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemoteServerSettings.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExpectedServerSettingsJson", DnsRemoteServerSettings.Script, StringComparison.Ordinal);
        Assert.Contains("Clear-DnsServerCache -Force", DnsRemoteServerSettings.Script, StringComparison.Ordinal);
        System.Management.Automation.Language.Parser.ParseInput(
            DnsRemoteServerSettings.Script, out _, out var parseErrors);
        Assert.Empty(parseErrors);
    }

    [Fact]
    public void Protocol_DnsPolicyMutationRequiresTypedCriteriaAndExpectedLiveState()
    {
        var valid = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsPolicyConfiguration,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 30,
            DnsPolicyAction = HostAgentDnsPolicyAction.SaveQueryPolicy,
            DnsExpectedPolicyConfigurationJson = "{}",
            DnsPolicyMutation = new()
            {
                Name = "InternalClients", ZoneName = "example.local", Level = HostAgentDnsPolicyLevel.Zone,
                Decision = HostAgentDnsPolicyDecision.Allow, Condition = HostAgentDnsPolicyCondition.And,
                ProcessingOrder = 1,
                ClientSubnet = new() { Operator = HostAgentDnsPolicyMatchOperator.Eq, Values = ["Internal"] },
                ZoneScopes = [new() { Name = "InternalScope", Weight = 1 }],
            },
        };

        Assert.Empty(valid.Validate());
        Assert.NotEmpty((valid with { DnsExpectedPolicyConfigurationJson = null }).Validate());
        Assert.NotEmpty((valid with { DnsPolicyMutation = valid.DnsPolicyMutation with { ClientSubnet = null } }).Validate());
        Assert.NotEmpty((valid with { DnsPolicyMutation = valid.DnsPolicyMutation with { ProcessingOrder = 0 } }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnsPolicyUsesFixedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsPolicyConfiguration,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 30,
            DnsPolicyAction = HostAgentDnsPolicyAction.Read,
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.PolicyCallCount);
        Assert.True(response.DnsPolicyConfiguration!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnsPolicyScript_IsFixedParsesAndDoesNotAcceptExecutableText()
    {
        Assert.StartsWith("param(", DnsRemotePolicyConfiguration.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemotePolicyConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ScriptBlock", DnsRemotePolicyConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExpectedConfigurationJson", DnsRemotePolicyConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Add-DnsServerZoneScope", DnsRemotePolicyConfiguration.Script, StringComparison.Ordinal);
        System.Management.Automation.Language.Parser.ParseInput(DnsRemotePolicyConfiguration.Script, out _, out var errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void Protocol_DnssecMutationRequiresZoneExpectedStateAndBoundedKeys()
    {
        var valid = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnssecConfiguration,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 120,
            DnssecAction = HostAgentDnssecAction.RolloverKeys,
            DnssecZoneName = "example.local",
            DnssecKeyIds = [Guid.NewGuid()],
            DnsExpectedDnssecConfigurationJson = "{\"zones\":[]}",
        };

        Assert.Empty(valid.Validate());
        Assert.NotEmpty((valid with { DnssecZoneName = null }).Validate());
        Assert.NotEmpty((valid with { DnssecKeyIds = [] }).Validate());
        Assert.NotEmpty((valid with { DnssecKeyIds = [Guid.Empty] }).Validate());
        Assert.NotEmpty((valid with { DnsExpectedDnssecConfigurationJson = null }).Validate());
        Assert.NotEmpty((valid with { DnssecAction = HostAgentDnssecAction.Unsign, DnssecKeyIds = [Guid.NewGuid()] }).Validate());
    }

    [Fact]
    public void Protocol_DnssecResolverMutationsRequireTypedBoundedAnchorData()
    {
        var baseline = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnssecConfiguration,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 120,
            DnsExpectedDnssecConfigurationJson = "{\"zones\":[],\"resolver\":{}}",
        };

        Assert.Empty((baseline with { DnssecAction = HostAgentDnssecAction.SetValidationEnabled, DnssecValidationEnabled = true }).Validate());
        Assert.NotEmpty((baseline with { DnssecAction = HostAgentDnssecAction.SetValidationEnabled }).Validate());
        Assert.Empty((baseline with { DnssecAction = HostAgentDnssecAction.RetrieveRootTrustAnchor }).Validate());
        Assert.Empty((baseline with
        {
            DnssecAction = HostAgentDnssecAction.AddDsTrustAnchor, DnssecTrustPointName = "secure.example",
            DnssecCryptoAlgorithm = "RsaSha256", DnssecKeyTag = 1234, DnssecDigestType = "Sha256",
            DnssecDigest = new string('A', 64),
        }).Validate());
        Assert.NotEmpty((baseline with
        {
            DnssecAction = HostAgentDnssecAction.AddDsTrustAnchor, DnssecTrustPointName = "secure.example",
            DnssecCryptoAlgorithm = "Unknown", DnssecKeyTag = 70000, DnssecDigestType = "Sha256",
            DnssecDigest = "not-hex",
        }).Validate());
        Assert.Empty((baseline with
        {
            DnssecAction = HostAgentDnssecAction.AddDnsKeyTrustAnchor, DnssecTrustPointName = "secure.example",
            DnssecCryptoAlgorithm = "ECDsaP256Sha256", DnssecBase64Data = Convert.ToBase64String([1, 2, 3]),
        }).Validate());
        Assert.NotEmpty((baseline with
        {
            DnssecAction = HostAgentDnssecAction.RemoveTrustAnchorType, DnssecTrustPointName = "secure.example",
            DnssecTrustAnchorType = "Unknown",
        }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnssecUsesFixedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnssecConfiguration,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 30,
            DnssecAction = HostAgentDnssecAction.Read,
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.DnssecCallCount);
        Assert.True(response.DnssecConfiguration!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnssecScript_IsFixedParsesAndUsesTypedLifecycleAndTrustCmdlets()
    {
        Assert.StartsWith("param(", DnsRemoteDnssecConfiguration.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemoteDnssecConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ScriptBlock", DnsRemoteDnssecConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Add-DnsServerTrustAnchor", DnsRemoteDnssecConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Remove-DnsServerTrustAnchor", DnsRemoteDnssecConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Set-DnsServerSetting", DnsRemoteDnssecConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Invoke-DnsServerZoneSign", DnsRemoteDnssecConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Invoke-DnsServerZoneUnsign", DnsRemoteDnssecConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Invoke-DnsServerSigningKeyRollover", DnsRemoteDnssecConfiguration.Script, StringComparison.Ordinal);
        System.Management.Automation.Language.Parser.ParseInput(DnsRemoteDnssecConfiguration.Script, out _, out var errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void Protocol_DnsScavengingMutationsRequireExpectedStateAndTypedBounds()
    {
        var baseline = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsScavenging,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 120,
            DnsExpectedScavengingConfigurationJson = "{\"zones\":[]}",
        };

        Assert.Empty((baseline with
        {
            DnsScavengingAction = HostAgentDnsScavengingAction.UpdateServer,
            DnsScavengingState = true, DnsScavengingIntervalHours = 168,
        }).Validate());
        Assert.NotEmpty((baseline with
        {
            DnsScavengingAction = HostAgentDnsScavengingAction.UpdateServer,
            DnsScavengingState = true, DnsScavengingIntervalHours = 0,
        }).Validate());
        Assert.Empty((baseline with
        {
            DnsScavengingAction = HostAgentDnsScavengingAction.UpdateZone,
            DnsAgingZoneName = "example.local", DnsZoneAgingEnabled = true,
            DnsZoneNoRefreshIntervalHours = 168, DnsZoneRefreshIntervalHours = 168,
            DnsZoneScavengeServers = ["192.0.2.10"],
        }).Validate());
        Assert.NotEmpty((baseline with
        {
            DnsScavengingAction = HostAgentDnsScavengingAction.UpdateZone,
            DnsAgingZoneName = "example.local", DnsZoneAgingEnabled = true,
            DnsZoneNoRefreshIntervalHours = 9000, DnsZoneRefreshIntervalHours = 168,
        }).Validate());
        Assert.NotEmpty((baseline with
        {
            DnsScavengingAction = HostAgentDnsScavengingAction.UpdateZone,
            DnsAgingZoneName = "example.local", DnsZoneAgingEnabled = true,
            DnsZoneNoRefreshIntervalHours = 168, DnsZoneRefreshIntervalHours = 168,
            DnsZoneScavengeServers = ["not-an-ip"],
        }).Validate());
        Assert.NotEmpty((baseline with
        {
            DnsScavengingAction = HostAgentDnsScavengingAction.StartScavenging,
            DnsExpectedScavengingConfigurationJson = null,
        }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnsScavengingUsesFixedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsScavenging,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 30,
            DnsScavengingAction = HostAgentDnsScavengingAction.Read,
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.ScavengingCallCount);
        Assert.True(response.DnsScavengingConfiguration!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnsScavengingScript_IsFixedParsesAndUsesTypedCmdlets()
    {
        Assert.StartsWith("param(", DnsRemoteScavengingConfiguration.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemoteScavengingConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ScriptBlock", DnsRemoteScavengingConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DnsPassword", DnsRemoteScavengingConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Get-DnsServerScavenging", DnsRemoteScavengingConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Set-DnsServerScavenging", DnsRemoteScavengingConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Set-DnsServerZoneAging", DnsRemoteScavengingConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Start-DnsServerScavenging -Force", DnsRemoteScavengingConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("ExpectedConfigurationJson", DnsRemoteScavengingConfiguration.Script, StringComparison.Ordinal);
        System.Management.Automation.Language.Parser.ParseInput(DnsRemoteScavengingConfiguration.Script, out _, out var errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void Protocol_DnsNetworkMutationsRequireExpectedStateAndTypedAddresses()
    {
        var baseline = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsNetworkConfiguration,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 120,
            DnsExpectedNetworkConfigurationJson = "{\"rootHints\":[]}",
        };

        Assert.Empty((baseline with { DnsNetworkAction = HostAgentDnsNetworkAction.UpdateListeningAddresses, DnsListeningIpAddresses = ["192.0.2.53"] }).Validate());
        Assert.NotEmpty((baseline with { DnsNetworkAction = HostAgentDnsNetworkAction.UpdateListeningAddresses, DnsListeningIpAddresses = [] }).Validate());
        Assert.NotEmpty((baseline with { DnsNetworkAction = HostAgentDnsNetworkAction.UpdateListeningAddresses, DnsListeningIpAddresses = ["bad"] }).Validate());
        Assert.Empty((baseline with { DnsNetworkAction = HostAgentDnsNetworkAction.AddRootHint, DnsRootHintNameServer = "a.root-servers.net.", DnsRootHintIpAddresses = ["198.41.0.4", "2001:503:ba3e::2:30"] }).Validate());
        Assert.NotEmpty((baseline with { DnsNetworkAction = HostAgentDnsNetworkAction.AddRootHint, DnsRootHintNameServer = "bad name", DnsRootHintIpAddresses = ["198.41.0.4"] }).Validate());
        Assert.NotEmpty((baseline with { DnsNetworkAction = HostAgentDnsNetworkAction.UpdateRootHint, DnsRootHintNameServer = "a.root-servers.net", DnsRootHintIpAddresses = ["198.41.0.4"] }).Validate());
        Assert.Empty((baseline with { DnsNetworkAction = HostAgentDnsNetworkAction.RemoveRootHint, DnsRootHintNameServer = "a.root-servers.net" }).Validate());
        Assert.NotEmpty((baseline with { DnsNetworkAction = HostAgentDnsNetworkAction.RemoveRootHint, DnsRootHintNameServer = "a.root-servers.net", DnsExpectedNetworkConfigurationJson = null }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnsNetworkConfigurationUsesFixedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var request = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsNetworkConfiguration,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 30,
            DnsNetworkAction = HostAgentDnsNetworkAction.Read,
        };

        var response = await dispatcher.DispatchAsync(request.ToJson(), WebApplication());

        Assert.Equal(1, executor.NetworkConfigurationCallCount);
        Assert.True(response.DnsNetworkConfiguration!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("dns-svc", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnsNetworkConfigurationScript_IsFixedParsesAndUsesTypedCmdlets()
    {
        Assert.StartsWith("param(", DnsRemoteNetworkConfiguration.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemoteNetworkConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ScriptBlock", DnsRemoteNetworkConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DnsPassword", DnsRemoteNetworkConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Get-DnsServerSetting -All", DnsRemoteNetworkConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Set-DnsServerSetting -InputObject", DnsRemoteNetworkConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Get-DnsServerRootHint", DnsRemoteNetworkConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Add-DnsServerRootHint", DnsRemoteNetworkConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Remove-DnsServerRootHint", DnsRemoteNetworkConfiguration.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-DnsServerRootHint", DnsRemoteNetworkConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("ExpectedConfigurationJson", DnsRemoteNetworkConfiguration.Script, StringComparison.Ordinal);
        System.Management.Automation.Language.Parser.ParseInput(DnsRemoteNetworkConfiguration.Script, out _, out var errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void Protocol_DnsZoneTransfersRequireTypedModesAddressesAndExpectedState()
    {
        var baseline = new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsZoneTransfers,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 120,
            DnsZoneTransferAction = HostAgentDnsZoneTransferAction.Update,
            DnsZoneName = "example.com", DnsZoneTransferMode = HostAgentDnsZoneTransferMode.NoTransfer,
            DnsZoneNotifyMode = HostAgentDnsZoneNotifyMode.NoNotify,
            DnsExpectedZoneTransferConfigurationJson = "{\"zones\":[]}",
        };

        Assert.Empty(baseline.Validate());
        Assert.NotEmpty((baseline with { DnsZoneTransferMode = HostAgentDnsZoneTransferMode.TransferToSecureServers }).Validate());
        Assert.Empty((baseline with { DnsZoneTransferMode = HostAgentDnsZoneTransferMode.TransferToSecureServers, DnsZoneSecondaryServers = ["192.0.2.10"] }).Validate());
        Assert.NotEmpty((baseline with { DnsZoneNotifyMode = HostAgentDnsZoneNotifyMode.NotifyServers }).Validate());
        Assert.NotEmpty((baseline with { DnsZoneSecondaryServers = ["bad"] }).Validate());
        Assert.NotEmpty((baseline with { DnsExpectedZoneTransferConfigurationJson = null }).Validate());
    }

    [Fact]
    public async Task Dispatch_DnsZoneTransfersUsesFixedExecutorAndNeverEchoesCredentials()
    {
        var executor = new RecordingDnsProbeExecutor();
        var dispatcher = new HostAgentDispatcher(Authorization, new RecordingOperations(), dnsRemoteProbeExecutor: executor);
        var response = await dispatcher.DispatchAsync(new HostAgentRequest
        {
            Operation = HostAgentOperation.ManageDnsZoneTransfers,
            DnsHostName = "dns01.example.local", DnsPort = 5986,
            DnsAuthenticationMode = HostAgentDnsAuthenticationMode.Negotiate,
            DnsUserName = "EXAMPLE\\dns-svc", DnsPassword = "secret", DnsTimeoutSeconds = 30,
            DnsZoneTransferAction = HostAgentDnsZoneTransferAction.Read,
        }.ToJson(), WebApplication());

        Assert.Equal(1, executor.ZoneTransferCallCount);
        Assert.True(response.DnsZoneTransferConfiguration!.Success);
        Assert.DoesNotContain("secret", response.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void DnsZoneTransferScript_IsFixedParsesAndUsesTypedCmdlet()
    {
        Assert.StartsWith("param(", DnsRemoteZoneTransferConfiguration.Script.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-Expression", DnsRemoteZoneTransferConfiguration.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DnsPassword", DnsRemoteZoneTransferConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("Set-DnsServerPrimaryZone @parameters", DnsRemoteZoneTransferConfiguration.Script, StringComparison.Ordinal);
        Assert.Contains("ExpectedConfigurationJson", DnsRemoteZoneTransferConfiguration.Script, StringComparison.Ordinal);
        System.Management.Automation.Language.Parser.ParseInput(DnsRemoteZoneTransferConfiguration.Script, out _, out var errors);
        Assert.Empty(errors);
    }

    // ------------------------------------------------------------------------------------------
    // Configuration
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Settings_ValidConfigurationRoundTrips()
    {
        var settings = new HostAgentSettings
        {
            RepositoryUrl = "https://github.com/meteciftci/ITAdmin.git",
            Branch = "main",
            InstallRoot = @"C:\ITAdmin",
            DataRoot = @"C:\ProgramData\ITAdmin",
            AppPoolName = "Contoso-ITAdmin",
            UpdatesEnabled = true,
        };

        Assert.Empty(settings.Validate());
        Assert.Empty(settings.FindDisallowedSecretFields());

        var restored = HostAgentSettings.FromJson(settings.ToJson());
        Assert.NotNull(restored);
        Assert.Equal("main", restored!.Branch);
        Assert.True(restored.UpdatesEnabled);
        Assert.Equal("Contoso-ITAdmin", restored.AppPoolName);
        Assert.EndsWith(
            Path.Combine("src", "scripts", "deploy", "Deploy-ITAdmin.ps1"),
            restored.DeployScriptPath,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_UpdatesAreEnabledByDefault() =>
        // The repository is public; a host that must never self-update sets updatesEnabled: false.
        Assert.True(new HostAgentSettings().UpdatesEnabled);

    [Fact]
    public void Settings_MissingRepositoryOrRootsIsReported()
    {
        var problems = new HostAgentSettings { RepositoryUrl = "", InstallRoot = "", DataRoot = "" }.Validate();

        Assert.Contains(problems, problem => problem.Contains("repositoryUrl", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("installRoot", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("dataRoot", StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_DeployScriptStaysInsideTheCheckedOutSource()
    {
        // Nothing about the update path is a path a caller supplied - it is always the script that
        // shipped with whatever commit this host currently has checked out.
        var settings = new HostAgentSettings { InstallRoot = @"D:\Somewhere\ITAdmin" };

        Assert.StartsWith(settings.SourceRoot, settings.DeployScriptPath, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------------------------------

    private sealed class RecordingOperations : IHostAgentOperations
    {
        public List<HostAgentOperation> Invoked { get; } = [];

        private Task<HostAgentResponse> Record(HostAgentOperation operation, HostAgentRequest request)
        {
            Invoked.Add(operation);
            return Task.FromResult(HostAgentResponse.Ok("ok", request.CorrelationId));
        }

        public Task<HostAgentResponse> GetInstallationStatusAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.GetInstallationStatus, request);

        public Task<HostAgentResponse> CheckForUpdatesAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.CheckForUpdates, request);

        public Task<HostAgentResponse> RequestUpdateAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.RequestUpdate, request);

        public Task<HostAgentResponse> GetUpdateStatusAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.GetUpdateStatus, request);

        public Task<HostAgentResponse> RecycleApplicationPoolAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.RecycleApplicationPool, request);

        public Task<HostAgentResponse> GetHttpsStatusAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.GetHttpsStatus, request);

        public Task<HostAgentResponse> ConfigureHttpsAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.ConfigureHttps, request);

        public Task<HostAgentResponse> DisableHttpsAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.DisableHttps, request);

        public void LogOperationFailure(HostAgentOperation operation, Exception exception)
        {
        }

        public void ReconcileInterruptedOperation() => ReconcileCalls++;

        public int ReconcileCalls { get; private set; }
    }

    private sealed class RecordingDnsProbeExecutor : IDnsRemoteProbeExecutor
    {
        public int CallCount { get; private set; }
        public int InventoryCallCount { get; private set; }
        public int MutationCallCount { get; private set; }
        public int ZoneMutationCallCount { get; private set; }
        public int ServerSettingsCallCount { get; private set; }
        public int PolicyCallCount { get; private set; }
        public int DnssecCallCount { get; private set; }
        public int ScavengingCallCount { get; private set; }
        public int NetworkConfigurationCallCount { get; private set; }
        public int ZoneTransferCallCount { get; private set; }
        public Task<HostAgentDnsProbeResult> ProbeAsync(HostAgentRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HostAgentDnsProbeResult { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnsInventoryPage> ReadInventoryPageAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            InventoryCallCount++;
            return Task.FromResult(new HostAgentDnsInventoryPage { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnsRecordMutationResult> MutateRecordAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            MutationCallCount++;
            return Task.FromResult(new HostAgentDnsRecordMutationResult { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnsZoneMutationResult> MutateZoneAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            ZoneMutationCallCount++;
            return Task.FromResult(new HostAgentDnsZoneMutationResult { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnsServerSettingsResult> ManageServerSettingsAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            ServerSettingsCallCount++;
            return Task.FromResult(new HostAgentDnsServerSettingsResult { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnsPolicyConfigurationResult> ManagePolicyConfigurationAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            PolicyCallCount++;
            return Task.FromResult(new HostAgentDnsPolicyConfigurationResult { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnssecConfigurationResult> ManageDnssecConfigurationAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            DnssecCallCount++;
            return Task.FromResult(new HostAgentDnssecConfigurationResult { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnsScavengingConfigurationResult> ManageDnsScavengingAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            ScavengingCallCount++;
            return Task.FromResult(new HostAgentDnsScavengingConfigurationResult { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnsNetworkConfigurationResult> ManageDnsNetworkConfigurationAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            NetworkConfigurationCallCount++;
            return Task.FromResult(new HostAgentDnsNetworkConfigurationResult { Success = true, Message = "ok" });
        }

        public Task<HostAgentDnsZoneTransferConfigurationResult> ManageDnsZoneTransfersAsync(
            HostAgentRequest request, CancellationToken cancellationToken)
        {
            ZoneTransferCallCount++;
            return Task.FromResult(new HostAgentDnsZoneTransferConfigurationResult { Success = true, Message = "ok" });
        }
    }

    private sealed class ThrowingOperations(Exception exception) : IHostAgentOperations
    {
        public bool Logged { get; private set; }

        public Task<HostAgentResponse> GetInstallationStatusAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public Task<HostAgentResponse> CheckForUpdatesAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public Task<HostAgentResponse> RequestUpdateAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public Task<HostAgentResponse> GetUpdateStatusAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public Task<HostAgentResponse> RecycleApplicationPoolAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public Task<HostAgentResponse> GetHttpsStatusAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public Task<HostAgentResponse> ConfigureHttpsAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public Task<HostAgentResponse> DisableHttpsAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public void LogOperationFailure(HostAgentOperation operation, Exception failure) => Logged = true;

        public void ReconcileInterruptedOperation()
        {
        }
    }
}
