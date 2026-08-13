using ITAdmin.Deployment;
using ITAdmin.HostAgent;
using ITAdmin.HostAgent.Contracts;

namespace ITAdmin.UnitTests.Deployment;

/// <summary>
/// The privilege boundary between the ITAdmin web application and the privileged host agent.
///
/// <para>
/// These are security tests, not feature tests. The boundary is the reason the web application can
/// keep an unprivileged app pool identity while ITAdmin still updates itself and reconfigures IIS,
/// so its properties are asserted directly rather than inferred from how the code happens to be
/// wired today.
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
        foreach (var property in typeof(HostAgentRequest).GetProperties())
        {
            foreach (var forbidden in new[] { "path", "command", "script", "argument", "executable", "directory" })
            {
                Assert.False(
                    property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"HostAgentRequest.{property.Name} would let a caller steer a privileged operation.");
            }
        }
    }

    [Fact]
    public void Protocol_ResponseCarriesNoSecretOrInternalPathFields()
    {
        // Responses are rendered in the ITAdmin UI.
        foreach (var type in new[]
                 {
                     typeof(HostAgentResponse), typeof(HostAgentInstallationStatus),
                     typeof(HostAgentUpdateStatus), typeof(HostAgentAvailableRelease),
                 })
        {
            foreach (var property in type.GetProperties())
            {
                foreach (var forbidden in new[] { "key", "secret", "password", "token", "repositoryUrl", "path" })
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
    [InlineData(HostAgentOperation.ReconcileWebBindings)]
    [InlineData(HostAgentOperation.RecycleApplicationPool)]
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
            new HostAgentRequest { Operation = HostAgentOperation.RequestUpdate }.ToJson(),
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
            new HostAgentRequest
            {
                Operation = HostAgentOperation.RequestUpdate,
                TargetVersion = "2.0.0",
            }.ToJson(),
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
            @"fatal: could not read /ProgramData/ITAdmin/keys/deploy_key at git@github.com:contoso/itadmin.git"));
        var dispatcher = new HostAgentDispatcher(Authorization, operations);

        var response = await dispatcher.DispatchAsync(
            new HostAgentRequest { Operation = HostAgentOperation.CheckForUpdates }.ToJson(),
            WebApplication());

        Assert.Equal(HostAgentResponseStatus.Failed, response.Status);
        Assert.DoesNotContain("deploy_key", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com", response.Message, StringComparison.OrdinalIgnoreCase);
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

    // ------------------------------------------------------------------------------------------
    // Request validation
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("2.0.0; shutdown")]
    [InlineData(@"..\..\Windows\System32")]
    [InlineData("refs/heads/main")]
    [InlineData("")]
    [InlineData("latest")]
    public void Request_TargetVersionThatIsNotAPlainVersionIsRejected(string version)
    {
        // A version becomes a ref name and a directory name inside the privileged process.
        var problems = new HostAgentRequest
        {
            Operation = HostAgentOperation.RequestUpdate,
            TargetVersion = version,
        }.Validate();

        Assert.NotEmpty(problems);
    }

    [Fact]
    public void Request_PlainVersionIsAccepted() =>
        Assert.Empty(new HostAgentRequest
        {
            Operation = HostAgentOperation.RequestUpdate,
            TargetVersion = "2.1.0",
        }.Validate());

    [Fact]
    public void Request_EnablingHttpsWithoutACertificateIsRejected() =>
        Assert.NotEmpty(new HostAgentRequest
        {
            Operation = HostAgentOperation.ReconcileWebBindings,
            HostName = "itadmin.example.com",
            EnableHttps = true,
        }.Validate());

    [Fact]
    public void Request_RedirectWithoutHttpsIsRejected() =>
        // Would remove the only working way in.
        Assert.NotEmpty(new HostAgentRequest
        {
            Operation = HostAgentOperation.ReconcileWebBindings,
            HostName = "itadmin.example.com",
            EnableHttps = false,
            RedirectHttpToHttps = true,
        }.Validate());

    [Theory]
    [InlineData("not a hostname")]
    [InlineData("http://itadmin.example.com")]
    public void Request_MalformedHostNameIsRejected(string hostName) =>
        Assert.NotEmpty(new HostAgentRequest
        {
            Operation = HostAgentOperation.ReconcileWebBindings,
            HostName = hostName,
        }.Validate());

    [Fact]
    public void Request_ValidBindingReconciliationIsAccepted() =>
        Assert.Empty(new HostAgentRequest
        {
            Operation = HostAgentOperation.ReconcileWebBindings,
            HostName = "itadmin.example.com",
            EnableHttps = true,
            CertificateThumbprint = new string('A', 40),
            RedirectHttpToHttps = true,
        }.Validate());

    // ------------------------------------------------------------------------------------------
    // Configuration separation
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Settings_HoldTheKeysLocationButNeverTheKey()
    {
        var settings = new HostAgentSettings
        {
            RepositoryUrl = "git@github.com:contoso/itadmin.git",
            DeployKeyDirectory = @"C:\ProgramData\ITAdmin\keys",
            AppPoolName = AppPool,
        };

        Assert.Empty(settings.Validate());
        Assert.Empty(settings.FindDisallowedSecretFields());
        Assert.EndsWith("deploy_key", settings.DeployKeyPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_DeployKeyIsNotUnderTheWebRootOrAReleaseDirectory()
    {
        var settings = new HostAgentSettings
        {
            RepositoryUrl = "git@github.com:contoso/itadmin.git",
            DeployKeyDirectory = @"C:\ProgramData\ITAdmin\keys",
            AppPoolName = AppPool,
        };

        var layout = new DeploymentLayout(settings.ProgramFilesRoot, settings.ProgramDataRoot);

        // A key inside a release directory would be served by IIS and readable by the app pool.
        Assert.False(settings.DeployKeyPath.StartsWith(layout.ReleasesRoot, StringComparison.OrdinalIgnoreCase));
        Assert.False(settings.DeployKeyPath.StartsWith(layout.SecretsRoot, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Settings_UpdatesAreDisabledUntilDeliberatelyEnabled() =>
        // A freshly installed host should not be persuadable into replacing its own release.
        Assert.False(new HostAgentSettings().UpdatesEnabled);

    [Fact]
    public void Settings_MissingRepositoryOrKeyLocationIsReported()
    {
        var problems = new HostAgentSettings().Validate();

        Assert.Contains(problems, problem => problem.Contains("repositoryUrl", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("deployKeyDirectory", StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_RoundTripThroughTheirStoredForm()
    {
        var settings = new HostAgentSettings
        {
            RepositoryUrl = "git@github.com:contoso/itadmin.git",
            DeployKeyDirectory = @"C:\ProgramData\ITAdmin\keys",
            AppPoolName = "Contoso-ITAdmin",
            Channel = ReleaseChannel.Preview,
            UpdatesEnabled = true,
        };

        var restored = HostAgentSettings.FromJson(settings.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(ReleaseChannel.Preview, restored!.Channel);
        Assert.True(restored.UpdatesEnabled);
        Assert.Equal("Contoso-ITAdmin", restored.AppPoolName);
    }

    [Fact]
    public void GitClient_PinsTheDeployKeyAndKeepsHostKeyCheckingOn()
    {
        var command = RepositoryAccessContract.BuildSshCommand(
            @"C:\ProgramData\ITAdmin\keys\deploy_key",
            @"C:\ProgramData\ITAdmin\keys\known_hosts");

        // IdentitiesOnly: otherwise SSH may offer an agent or user key, making the installation
        // depend on whichever account happened to run it.
        Assert.Contains("IdentitiesOnly=yes", command, StringComparison.Ordinal);
        // BatchMode: a prompt would hang a service with no console forever.
        Assert.Contains("BatchMode=yes", command, StringComparison.Ordinal);
        Assert.Contains("StrictHostKeyChecking=yes", command, StringComparison.Ordinal);
        Assert.DoesNotContain("StrictHostKeyChecking=no", command, StringComparison.Ordinal);
        Assert.DoesNotContain("StrictHostKeyChecking=accept-new", command, StringComparison.Ordinal);

        // The machine's own known_hosts, not an administrator's profile, and no system-wide file
        // that could silently widen what the service trusts.
        Assert.Contains(@"UserKnownHostsFile=""C:\ProgramData\ITAdmin\keys\known_hosts""", command, StringComparison.Ordinal);
        Assert.Contains("GlobalKnownHostsFile=/dev/null", command, StringComparison.Ordinal);
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

        public Task<HostAgentResponse> ReconcileWebBindingsAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.ReconcileWebBindings, request);

        public Task<HostAgentResponse> RecycleApplicationPoolAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            Record(HostAgentOperation.RecycleApplicationPool, request);

        public void LogOperationFailure(HostAgentOperation operation, Exception exception)
        {
        }

        public void ReconcileInterruptedOperation() => ReconcileCalls++;

        public int ReconcileCalls { get; private set; }
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

        public Task<HostAgentResponse> ReconcileWebBindingsAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public Task<HostAgentResponse> RecycleApplicationPoolAsync(HostAgentRequest request, CancellationToken cancellationToken) =>
            throw exception;

        public void LogOperationFailure(HostAgentOperation operation, Exception failure) => Logged = true;

        public void ReconcileInterruptedOperation()
        {
        }
    }
}
