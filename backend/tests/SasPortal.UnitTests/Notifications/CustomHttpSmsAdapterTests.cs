using Microsoft.Extensions.Logging.Abstractions;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Common.Models.Notifications;
using SasPortal.Infrastructure.Notifications.Sms;

namespace SasPortal.UnitTests.Notifications;

public sealed class CustomHttpSmsAdapterTests
{
    [Fact]
    public async Task ValidateAsync_InvalidEndpoint_ReturnsFailure()
    {
        var adapter = new CustomHttpSmsAdapter(new FakeHttpClientFactory(), NullLogger<CustomHttpSmsAdapter>.Instance);
        var settings = new SmsProviderRuntimeSettings(
            new SmsCustomHttpPublicSettings
            {
                EndpointUrl = "not-a-url",
                Method = "POST",
                ContentType = "application/json",
                AuthType = "None",
                TimeoutSeconds = 30,
            },
            new SmsCustomHttpSecretSettings());

        var result = await adapter.ValidateAsync(settings);

        Assert.False(result.IsSuccess);
        Assert.Contains("Endpoint URL", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_BearerWithoutToken_ReturnsFailure()
    {
        var adapter = new CustomHttpSmsAdapter(new FakeHttpClientFactory(), NullLogger<CustomHttpSmsAdapter>.Instance);
        var settings = new SmsProviderRuntimeSettings(
            new SmsCustomHttpPublicSettings
            {
                EndpointUrl = "https://example.com/sms",
                Method = "POST",
                ContentType = "application/json",
                AuthType = "BearerToken",
                TimeoutSeconds = 30,
            },
            new SmsCustomHttpSecretSettings());

        var result = await adapter.ValidateAsync(settings);

        Assert.False(result.IsSuccess);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }
}
