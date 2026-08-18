using System.Reflection;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.SystemUpdates;
using ITAdmin.Api.Controllers;
using ITAdmin.Api.HostAgent;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Security;
using ITAdmin.HostAgent.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITAdmin.UnitTests.SystemUpdates;

public sealed class SystemUpdatesControllerTests
{
    [Fact]
    public void StatusAndCheck_RequireView_WhileInstallRequiresManage()
    {
        AssertPermission(nameof(SystemUpdatesController.GetStatus), PermissionCodes.SystemUpdates.View);
        AssertPermission(nameof(SystemUpdatesController.CheckForUpdates), PermissionCodes.SystemUpdates.View);
        AssertPermission(nameof(SystemUpdatesController.Install), PermissionCodes.SystemUpdates.Manage);
    }

    [Fact]
    public async Task Install_WithoutDatabaseBackupConfirmation_IsRejectedBeforeCallingTheAgent()
    {
        var agent = new RecordingHostAgentClient();
        var controller = new SystemUpdatesController(
            agent,
            new NoOpAuditLogWriter(),
            NullLogger<SystemUpdatesController>.Instance);

        var result = await controller.Install(
            new InstallSystemUpdateRequest("2.0.0", DatabaseBackupConfirmed: false),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, agent.CallCount);
    }

    private static void AssertPermission(string method, string permission)
    {
        var attribute = typeof(SystemUpdatesController)
            .GetMethod(method)!
            .GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal($"Permission:{permission}", attribute!.Policy);
    }

    private sealed class RecordingHostAgentClient : IHostAgentClient
    {
        public int CallCount { get; private set; }

        public Task<HostAgentResponse> SendAsync(
            HostAgentRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(HostAgentResponse.Failed("Unexpected call.", request.CorrelationId));
        }
    }

    private sealed class NoOpAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(AuditLogWriteRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
