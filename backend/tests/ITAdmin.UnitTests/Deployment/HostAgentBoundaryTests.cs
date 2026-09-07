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
                     typeof(HostAgentHttpsStatus),
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
