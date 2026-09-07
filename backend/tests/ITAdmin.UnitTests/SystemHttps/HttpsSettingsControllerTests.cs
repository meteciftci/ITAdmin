using System.Reflection;
using System.Text;
using ITAdmin.Api.Authorization;
using ITAdmin.Api.Contracts.SystemHttps;
using ITAdmin.Api.Controllers;
using ITAdmin.Api.HostAgent;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Security;
using ITAdmin.HostAgent.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITAdmin.UnitTests.SystemHttps;

public sealed class HttpsSettingsControllerTests
{
    [Fact]
    public void StatusRequiresView_ConfigureAndDisableRequireManage()
    {
        AssertPermission(nameof(HttpsSettingsController.GetStatus), PermissionCodes.SystemHttps.View);
        AssertPermission(nameof(HttpsSettingsController.Configure), PermissionCodes.SystemHttps.Manage);
        AssertPermission(nameof(HttpsSettingsController.Disable), PermissionCodes.SystemHttps.Manage);
    }

    [Fact]
    public async Task Configure_WithoutAPfx_IsRejectedBeforeCallingTheAgent()
    {
        var agent = new RecordingHostAgentClient();
        var controller = NewController(agent);

        var result = await controller.Configure(pfx: null, password: "x", httpsPort: 443, redirectHttpToHttps: true, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, agent.CallCount);
    }

    [Fact]
    public async Task Configure_WithAnOutOfRangePort_IsRejectedBeforeCallingTheAgent()
    {
        var agent = new RecordingHostAgentClient();
        var controller = NewController(agent);

        var result = await controller.Configure(FakePfx(), password: "", httpsPort: 70_000, redirectHttpToHttps: false, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, agent.CallCount);
    }

    [Fact]
    public async Task Configure_ForwardsThePfxAsBase64AndReturnsTheAgentStatus()
    {
        var agent = new RecordingHostAgentClient(new HostAgentResponse
        {
            Status = HostAgentResponseStatus.Ok,
            Message = "HTTPS is bound on port 443.",
            Https = new HostAgentHttpsStatus { Enabled = true, Port = 443, CertificateThumbprint = "ABC123" },
        });
        var controller = NewController(agent);

        var result = await controller.Configure(FakePfx(), password: "pfx-pw", httpsPort: 443, redirectHttpToHttps: true, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<ConfigureSystemHttpsResponse>(ok.Value);
        Assert.True(body.Enabled);
        Assert.Equal("ABC123", body.CertificateThumbprint);

        Assert.Equal(1, agent.CallCount);
        Assert.Equal(HostAgentOperation.ConfigureHttps, agent.LastRequest!.Operation);
        Assert.False(string.IsNullOrWhiteSpace(agent.LastRequest.PfxBase64));
        Assert.Equal("pfx-pw", agent.LastRequest.PfxPassword);
        Assert.Equal(443, agent.LastRequest.HttpsPort);
        Assert.True(agent.LastRequest.RedirectHttpToHttps);
    }

    private static HttpsSettingsController NewController(IHostAgentClient agent)
    {
        var controller = new HttpsSettingsController(agent, new NoOpAuditLogWriter(), NullLogger<HttpsSettingsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return controller;
    }

    private static IFormFile FakePfx()
    {
        var bytes = Encoding.ASCII.GetBytes("not-a-real-pfx-but-non-empty");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "pfx", "server.pfx");
    }

    private static void AssertPermission(string method, string permission)
    {
        var attribute = typeof(HttpsSettingsController).GetMethod(method)!.GetCustomAttribute<RequirePermissionAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal($"Permission:{permission}", attribute!.Policy);
    }

    private sealed class RecordingHostAgentClient(HostAgentResponse? response = null) : IHostAgentClient
    {
        public int CallCount { get; private set; }
        public HostAgentRequest? LastRequest { get; private set; }

        public Task<HostAgentResponse> SendAsync(HostAgentRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(response ?? HostAgentResponse.Ok("ok", request.CorrelationId));
        }
    }

    private sealed class NoOpAuditLogWriter : IAuditLogWriter
    {
        public Task WriteAsync(AuditLogWriteRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
