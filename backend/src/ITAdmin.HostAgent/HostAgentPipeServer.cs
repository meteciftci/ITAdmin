using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using ITAdmin.HostAgent.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITAdmin.HostAgent;

/// <summary>
/// Serves the named pipe the ITAdmin web application talks to.
///
/// <para>
/// The ACL below is the primary security control, and it is deliberately an allow-list of three
/// principals: SYSTEM (the agent itself), the local Administrators group, and the ITAdmin
/// application pool identity. Everything else - other services, interactive users, scheduled tasks
/// - cannot open the pipe at all, so the dispatcher never sees them. Authorization inside the
/// process then narrows further, because being able to connect is not the same as being allowed to
/// trigger an update.
/// </para>
///
/// <para>
/// Frames are length-prefixed and bounded. A privileged reader that trusts a caller-supplied length
/// is a denial-of-service at best, so the length is validated before a single byte is allocated.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HostAgentPipeServer(
    HostAgentDispatcher dispatcher,
    HostAgentAuthorization authorization,
    IHostAgentOperations operations,
    ILogger<HostAgentPipeServer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Before accepting a single request, work out what the previous run of this service left
        // behind. A restart during an update must not leave a machine that cannot be classified.
        operations.ReconcileInterruptedOperation();

        logger.LogInformation(
            "ITAdmin Host Agent listening on pipe {Pipe} for {Identity}.",
            HostAgentProtocol.PipeName,
            authorization.AppPoolIdentity);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ServeOneConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Host agent connection failed.");

                // Never spin: a persistent failure (pipe name taken, ACL problem) would otherwise
                // burn a core and flood the event log.
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task ServeOneConnectionAsync(CancellationToken stoppingToken)
    {
        // NamedPipeServerStreamAcl is the only way to create the pipe with its ACL already applied.
        // Creating it first and securing it afterwards would leave a window in which an unintended
        // process could connect to a privileged endpoint.
        using var server = NamedPipeServerStreamAcl.Create(
            HostAgentProtocol.PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 4,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough,
            inBufferSize: 0,
            outBufferSize: 0,
            BuildPipeSecurity(authorization.AppPoolIdentity, logger));

        await server.WaitForConnectionAsync(stoppingToken);

        try
        {
            var caller = ResolveCaller(server);
            var requestJson = await ReadFrameAsync(server, stoppingToken);

            var response = await dispatcher.DispatchAsync(requestJson, caller, stoppingToken);
            await WriteFrameAsync(server, response.ToJson(), stoppingToken);
        }
        finally
        {
            if (server.IsConnected)
            {
                server.Disconnect();
            }
        }
    }

    /// <summary>
    /// Identifies the connected principal.
    ///
    /// <para>
    /// The name comes from the pipe itself, not from anything the caller sent, which is exactly why
    /// this transport was chosen. If the identity cannot be established the caller is treated as
    /// unknown and non-administrative, and the authorization rules deny it.
    /// </para>
    /// </summary>
    private HostAgentCallerContext ResolveCaller(NamedPipeServerStream server)
    {
        try
        {
            var identityName = server.GetImpersonationUserName();
            var isAdministrator = false;

            server.RunAsClient(() =>
            {
                using var identity = WindowsIdentity.GetCurrent();
                isAdministrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            });

            return new HostAgentCallerContext(identityName, isAdministrator);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not identify the pipe caller; treating it as unknown.");
            return new HostAgentCallerContext(null, false);
        }
    }

    /// <summary>
    /// The pipe ACL. Built explicitly rather than inherited, so the set of principals that can
    /// reach a privileged service is written down in one readable place.
    /// </summary>
    internal static PipeSecurity BuildPipeSecurity(string appPoolIdentity, ILogger logger)
    {
        var security = new PipeSecurity();

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        try
        {
            // The app pool virtual account exists only once IIS has created the pool. If it has
            // not, the agent still starts and serves administrators - it simply cannot be reached
            // by an application that does not yet exist.
            security.AddAccessRule(new PipeAccessRule(
                new NTAccount(appPoolIdentity),
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
        }
        catch (IdentityNotMappedException)
        {
            logger.LogWarning(
                "Application pool identity {Identity} could not be resolved; the ITAdmin application "
                + "will not be able to reach the host agent until the pool exists.",
                appPoolIdentity);
        }

        return security;
    }

    private static async Task<string> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken);
        var length = BitConverter.ToInt32(lengthBuffer);

        if (length is <= 0 or > HostAgentProtocol.MaxFrameBytes)
        {
            throw new InvalidDataException($"Rejected a {length}-byte frame.");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return Encoding.UTF8.GetString(payload);
    }

    private static async Task WriteFrameAsync(Stream stream, string json, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(BitConverter.GetBytes(payload.Length), cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
