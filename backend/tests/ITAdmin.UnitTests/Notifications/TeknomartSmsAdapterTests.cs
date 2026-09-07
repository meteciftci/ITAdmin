using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.Notifications;
using ITAdmin.Infrastructure.Notifications.Sms;

namespace ITAdmin.UnitTests.Notifications;

public sealed class TeknomartSmsAdapterTests
{
    private const string OkBody = """{"data":{"pkgID":42},"err":null}""";

    [Fact]
    public async Task SendAsync_Otp_PostsToTheOtpEndpointWithBasicAuth()
    {
        var handler = new CapturingHandler(_ => Json(OkBody));
        var adapter = CreateAdapter(handler);

        var result = await adapter.SendAsync(
            new SmsSendRequest("905551234567", "Kodunuz 1234", SmsSendKind.Otp),
            Settings(defaultKind: "Single"));

        Assert.True(result.IsSuccess);
        Assert.EndsWith("/sms/create-otp", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Basic", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("apiuser:secret")),
            handler.LastRequest.Headers.Authorization.Parameter);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("905551234567", doc.RootElement.GetProperty("number").GetString());
        Assert.Equal("ITADMIN", doc.RootElement.GetProperty("sender").GetString());
        Assert.False(doc.RootElement.TryGetProperty("title", out _));
        Assert.Contains("pkgID 42", result.ProviderSummary);
    }

    [Fact]
    public async Task SendAsync_Single_PostsToTheCreateEndpointWithTitleAndFixedFields()
    {
        var handler = new CapturingHandler(_ => Json(OkBody));
        var adapter = CreateAdapter(handler);

        var result = await adapter.SendAsync(
            new SmsSendRequest("905551234567", "Bilgilendirme", SmsSendKind.Single),
            Settings(singleTitle: "ITAdmin-Portal"));

        Assert.True(result.IsSuccess);
        Assert.EndsWith("/sms/create", handler.LastRequest!.RequestUri!.AbsolutePath);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(1, doc.RootElement.GetProperty("type").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("sendingType").GetInt32());
        Assert.Equal("ITAdmin-Portal", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SendAsync_DefaultKind_FallsBackToConfiguredDefault()
    {
        var handler = new CapturingHandler(_ => Json(OkBody));
        var adapter = CreateAdapter(handler);

        await adapter.SendAsync(
            new SmsSendRequest("905551234567", "x", SmsSendKind.Default),
            Settings(defaultKind: "Otp"));

        Assert.EndsWith("/sms/create-otp", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SendAsync_BlankBaseUrl_FallsBackToTheDefaultHost()
    {
        var handler = new CapturingHandler(_ => Json(OkBody));
        var adapter = CreateAdapter(handler);

        await adapter.SendAsync(
            new SmsSendRequest("905551234567", "x", SmsSendKind.Otp),
            Settings(baseUrl: ""));

        Assert.Equal(
            new Uri(TeknomartSmsAdapter.DefaultBaseUrl + "/sms/create-otp"),
            handler.LastRequest!.RequestUri);
    }

    [Theory]
    [InlineData("https://app.teknomart.com.tr:9588")]
    [InlineData("https://app.teknomart.com.tr:9588/")]
    [InlineData("https://app.teknomart.com.tr:9588/sms/create-otp")]
    public async Task SendAsync_BaseUrlWithOrWithoutAnEndpointPath_TargetsTheAuthorityPlusEndpoint(string configured)
    {
        var handler = new CapturingHandler(_ => Json(OkBody));
        var adapter = CreateAdapter(handler);

        await adapter.SendAsync(
            new SmsSendRequest("905551234567", "x", SmsSendKind.Otp),
            Settings(baseUrl: configured));

        Assert.Equal(
            new Uri("https://app.teknomart.com.tr:9588/sms/create-otp"),
            handler.LastRequest!.RequestUri);
    }

    [Fact]
    public async Task ValidateAsync_BlankBaseUrl_IsAllowed()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => Json(OkBody)));

        var result = await adapter.ValidateAsync(Settings(baseUrl: "", defaultKind: "Otp", singleTitle: null));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SendAsync_ProviderError_IsSurfacedWithCodeAndMessage()
    {
        var handler = new CapturingHandler(_ => Json(
            """{"data":null,"err":{"code":"ERR_USER_CREDIT_REQUIRED","status":417,"message":"Yetersiz kredi"}}""",
            HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);

        var result = await adapter.SendAsync(
            new SmsSendRequest("905551234567", "x", SmsSendKind.Otp),
            Settings());

        Assert.False(result.IsSuccess);
        Assert.Contains("Yetersiz kredi", result.Message);
        Assert.Contains("ERR_USER_CREDIT_REQUIRED", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_MissingSingleTitle_FailsWhenSingleIsReachable()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => Json(OkBody)));

        var result = await adapter.ValidateAsync(Settings(defaultKind: "Single", singleTitle: null));

        Assert.False(result.IsSuccess);
        Assert.Contains("title", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_OtpOnly_DoesNotRequireSingleTitle()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => Json(OkBody)));

        var result = await adapter.ValidateAsync(Settings(defaultKind: "Otp", singleTitle: null));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidateAsync_MissingCredentials_Fails()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => Json(OkBody)));

        var result = await adapter.ValidateAsync(new SmsProviderRuntimeSettings(
            NotificationProviderKeys.Teknomart,
            JsonSerializer.Serialize(new SmsTeknomartPublicSettings
            {
                BaseUrl = "https://api.teknomart.com.tr:9588",
                Sender = "ITADMIN",
                DefaultSmsKind = "Otp",
            }),
            "{}"));

        Assert.False(result.IsSuccess);
        Assert.Contains("username", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TeknomartSmsAdapter CreateAdapter(HttpMessageHandler handler) =>
        new(new FakeHttpClientFactory(handler), NullLogger<TeknomartSmsAdapter>.Instance);

    private static SmsProviderRuntimeSettings Settings(
        string defaultKind = "Single",
        string? singleTitle = "ITAdmin-Portal",
        string? baseUrl = "https://api.teknomart.com.tr:9588") =>
        new(
            NotificationProviderKeys.Teknomart,
            JsonSerializer.Serialize(new SmsTeknomartPublicSettings
            {
                IsEnabled = true,
                BaseUrl = baseUrl,
                Sender = "ITADMIN",
                DefaultSmsKind = defaultKind,
                SingleSmsTitle = singleTitle,
                TimeoutSeconds = 30,
            }),
            JsonSerializer.Serialize(new SmsTeknomartSecretSettings { Username = "apiuser", Password = "secret" }));

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
