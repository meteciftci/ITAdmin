using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Infrastructure.Notifications.Email;

public sealed partial class SmtpEmailProviderAdapter(ILogger<SmtpEmailProviderAdapter> logger) : IEmailProviderAdapter
{
    private static readonly Regex EmailRegex = EmailAddressRegex();

    public string ProviderKey => NotificationProviderKeys.Smtp;
    public string DisplayName => "SMTP";

    public EmailProviderDefinition GetDefinition() => new(ProviderKey, DisplayName);

    public Task<EmailSendResult> ValidateAsync(
        EmailProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateSettings(settings);
        return Task.FromResult(validationError is null
            ? new EmailSendResult(true, "Email provider settings are valid.")
            : new EmailSendResult(false, validationError));
    }

    public async Task<EmailSendResult> SendAsync(
        EmailSendRequest request,
        EmailProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateSettings(settings);
        if (validationError is not null)
        {
            return new EmailSendResult(false, validationError);
        }

        if (string.IsNullOrWhiteSpace(request.RecipientEmail)
            || !EmailRegex.IsMatch(request.RecipientEmail.Trim()))
        {
            return new EmailSendResult(false, "Recipient email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return new EmailSendResult(false, "Subject is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return new EmailSendResult(false, "Body is required.");
        }

        try
        {
            using var message = BuildMessage(request, settings);
            using var client = CreateClient(settings);
            await client.SendMailAsync(message, cancellationToken);
            return new EmailSendResult(true, "Email sent successfully.");
        }
        catch (SmtpException exception)
        {
            logger.LogWarning(exception, "SMTP send failed.");
            return new EmailSendResult(false, "Email could not be sent. Check SMTP settings and credentials.");
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("SMTP send timed out.");
            return new EmailSendResult(false, "Email provider request timed out.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "SMTP send failed unexpectedly.");
            return new EmailSendResult(false, "Email could not be sent.");
        }
    }

    internal static string? ValidateSettings(EmailProviderRuntimeSettings settings)
    {
        var publicSettings = settings.Public;

        if (string.IsNullOrWhiteSpace(publicSettings.Host))
        {
            return "SMTP host is required.";
        }

        if (publicSettings.Port is < 1 or > 65535)
        {
            return "SMTP port must be between 1 and 65535.";
        }

        if (string.IsNullOrWhiteSpace(publicSettings.FromAddress)
            || !EmailRegex.IsMatch(publicSettings.FromAddress.Trim()))
        {
            return "From address must be a valid email.";
        }

        if (publicSettings.TimeoutSeconds is < 5 or > 300)
        {
            return "Timeout must be between 5 and 300 seconds.";
        }

        var hasPassword = !string.IsNullOrWhiteSpace(settings.Secrets.Password);
        var hasUserName = !string.IsNullOrWhiteSpace(publicSettings.UserName);
        if (hasPassword && !hasUserName)
        {
            return "SMTP username is required when password is configured.";
        }

        return null;
    }

    private static MailMessage BuildMessage(EmailSendRequest request, EmailProviderRuntimeSettings settings)
    {
        var fromAddress = settings.Public.FromAddress!.Trim();
        var fromDisplayName = settings.Public.FromDisplayName?.Trim();
        var from = string.IsNullOrWhiteSpace(fromDisplayName)
            ? new MailAddress(fromAddress)
            : new MailAddress(fromAddress, fromDisplayName);

        var message = new MailMessage
        {
            From = from,
            Subject = request.Subject.Trim(),
            Body = request.Body,
            IsBodyHtml = false,
        };
        message.To.Add(request.RecipientEmail.Trim());
        return message;
    }

    private static SmtpClient CreateClient(EmailProviderRuntimeSettings settings)
    {
        var publicSettings = settings.Public;
        var client = new SmtpClient(publicSettings.Host!.Trim(), publicSettings.Port)
        {
            EnableSsl = publicSettings.UseSsl,
            Timeout = publicSettings.TimeoutSeconds * 1000,
        };

        if (!string.IsNullOrWhiteSpace(publicSettings.UserName)
            && !string.IsNullOrWhiteSpace(settings.Secrets.Password))
        {
            client.Credentials = new NetworkCredential(
                publicSettings.UserName.Trim(),
                settings.Secrets.Password);
        }

        return client;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EmailAddressRegex();
}
