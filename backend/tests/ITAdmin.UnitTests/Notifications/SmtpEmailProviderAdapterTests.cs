using Microsoft.Extensions.Logging.Abstractions;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Common.Models.Notifications;
using ITAdmin.Infrastructure.Notifications.Email;

namespace ITAdmin.UnitTests.Notifications;

public sealed class SmtpEmailProviderAdapterTests
{
    [Fact]
    public async Task ValidateAsync_MissingHost_ReturnsFailure()
    {
        var adapter = new SmtpEmailProviderAdapter(NullLogger<SmtpEmailProviderAdapter>.Instance);
        var settings = new EmailProviderRuntimeSettings(
            new EmailSmtpPublicSettings
            {
                FromAddress = "noreply@example.com",
                Port = 587,
                TimeoutSeconds = 30,
            },
            new EmailSmtpSecretSettings());

        var result = await adapter.ValidateAsync(settings);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ValidateAsync_PasswordWithoutUsername_ReturnsFailure()
    {
        var adapter = new SmtpEmailProviderAdapter(NullLogger<SmtpEmailProviderAdapter>.Instance);
        var settings = new EmailProviderRuntimeSettings(
            new EmailSmtpPublicSettings
            {
                Host = "smtp.example.com",
                FromAddress = "noreply@example.com",
                Port = 587,
                TimeoutSeconds = 30,
            },
            new EmailSmtpSecretSettings { Password = "secret" });

        var result = await adapter.ValidateAsync(settings);

        Assert.False(result.IsSuccess);
        Assert.Contains("username", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
