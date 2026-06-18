using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Common.Models.Notifications;
using ITAdmin.Infrastructure.Notifications;
using ITAdmin.Infrastructure.Notifications.Sms;

namespace ITAdmin.UnitTests.Notifications;

public sealed class CustomHttpSmsAdapterTests
{
    private const string JsonBodyTemplate = """
        {
          "type": 1,
          "content": "{{message}}",
          "number": "{{phone}}",
          "sender": "{{sender}}"
        }
        """;

    [Fact]
    public async Task ValidateAsync_InvalidEndpoint_ReturnsFailure()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var settings = CreateSettings(endpointUrl: "not-a-url");

        var result = await adapter.ValidateAsync(settings);

        Assert.False(result.IsSuccess);
        Assert.Contains("Endpoint URL", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_BearerWithoutToken_ReturnsFailure()
    {
        var adapter = CreateAdapter(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var settings = CreateSettings(authType: "BearerToken");

        var result = await adapter.ValidateAsync(settings);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task SendAsync_JsonBodyMessageContainsUrl_SendsValidJson()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);
        var message = "Kullanıcınız oluşturuldu: https://portal.local/login";

        var result = await adapter.SendAsync(
            new SmsSendRequest("5551234567", message),
            CreateSettings(bodyTemplate: JsonBodyTemplate, sender: "ITAdmin"));

        Assert.True(result.IsSuccess);
        Assert.True(handler.WasCalled);
        Assert.NotNull(handler.LastBody);
        using var document = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(message, document.RootElement.GetProperty("content").GetString());
        Assert.Equal("5551234567", document.RootElement.GetProperty("number").GetString());
    }

    [Fact]
    public async Task SendAsync_JsonBodyMessageContainsNewline_SendsValidJsonWithNewline()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);
        var message = $"Satır 1{Environment.NewLine}Satır 2";

        var result = await adapter.SendAsync(
            new SmsSendRequest("5551234567", message),
            CreateSettings(bodyTemplate: JsonBodyTemplate));

        Assert.True(result.IsSuccess);
        using var document = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(message, document.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_JsonBodyMessageContainsDoubleQuote_SendsValidJson()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);
        var message = "Sayın \"Çağrı\", hesabınız oluşturuldu.";

        var result = await adapter.SendAsync(
            new SmsSendRequest("5551234567", message),
            CreateSettings(bodyTemplate: JsonBodyTemplate));

        Assert.True(result.IsSuccess);
        using var document = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(message, document.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_JsonBodyMessageContainsBackslash_SendsValidJson()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);
        var message = @"C:\Temp\Portal";

        var result = await adapter.SendAsync(
            new SmsSendRequest("5551234567", message),
            CreateSettings(bodyTemplate: JsonBodyTemplate));

        Assert.True(result.IsSuccess);
        using var document = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(message, document.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendAsync_JsonBodyInvalidTemplate_DoesNotSendRequest()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);
        const string brokenTemplate = """
            {
              "content": "{{message}}",
            """;

        var result = await adapter.SendAsync(
            new SmsSendRequest("5551234567", "Test"),
            CreateSettings(bodyTemplate: brokenTemplate));

        Assert.False(result.IsSuccess);
        Assert.False(handler.WasCalled);
        Assert.Equal(
            NotificationTemplateReplacer.InvalidJsonBodyMessage,
            result.Message);
        Assert.Null(result.ProviderSummary);
    }

    [Fact]
    public async Task SendAsync_TextPlainBody_ReplacesMessageWithoutJsonEscaping()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);
        var message = "Line1\nLine2 \"quoted\"";

        var result = await adapter.SendAsync(
            new SmsSendRequest("5551234567", message),
            CreateSettings(
                contentType: "text/plain",
                bodyTemplate: "Phone={{phone}} Message={{message}}"));

        Assert.True(result.IsSuccess);
        Assert.Contains(message, handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\\n", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_BasicAuth_SetsAuthorizationHeader()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);

        var result = await adapter.SendAsync(
            new SmsSendRequest("5551234567", "Test"),
            CreateSettings(
                bodyTemplate: JsonBodyTemplate,
                authType: "Basic",
                basicUserName: "api-user",
                basicPassword: "secret-pass"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequest);
        Assert.NotNull(handler.LastRequest.Headers.Authorization);
        Assert.Equal("Basic", handler.LastRequest.Headers.Authorization!.Scheme);
        var expectedCredentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("api-user:secret-pass"));
        Assert.Equal(expectedCredentials, handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Theory]
    [InlineData("tab\there")]
    [InlineData("emoji \u263A test")]
    public async Task SendAsync_JsonBodySpecialCharacters_SendsValidJson(string message)
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var adapter = CreateAdapter(handler);

        var result = await adapter.SendAsync(
            new SmsSendRequest("5551234567", message),
            CreateSettings(bodyTemplate: JsonBodyTemplate));

        Assert.True(result.IsSuccess);
        using var document = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(message, document.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public void ToJsonStringInnerValue_EscapesWithoutSurroundingQuotes()
    {
        const string value = "a\"b\nc";
        var inner = NotificationTemplateReplacer.ToJsonStringInnerValue(value);
        var roundTrip = JsonSerializer.Deserialize<string>($"\"{inner}\"");
        Assert.Equal(value, roundTrip);
    }

    private static CustomHttpSmsAdapter CreateAdapter(HttpMessageHandler handler) =>
        new(new FakeHttpClientFactory(handler), NullLogger<CustomHttpSmsAdapter>.Instance);

    private static SmsProviderRuntimeSettings CreateSettings(
        string endpointUrl = "https://example.com/sms",
        string method = "POST",
        string contentType = "application/json",
        string authType = "None",
        string? bodyTemplate = null,
        string? sender = "SAS",
        string? basicUserName = null,
        string? basicPassword = null) =>
        new(
            new SmsCustomHttpPublicSettings
            {
                EndpointUrl = endpointUrl,
                Method = method,
                ContentType = contentType,
                AuthType = authType,
                BodyTemplate = bodyTemplate ?? JsonBodyTemplate,
                Sender = sender,
                TimeoutSeconds = 30,
                SuccessStatusCodes = [200],
            },
            new SmsCustomHttpSecretSettings
            {
                BasicUserName = basicUserName,
                BasicPassword = basicPassword,
            });

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }
}
