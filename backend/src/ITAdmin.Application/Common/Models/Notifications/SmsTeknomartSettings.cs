namespace ITAdmin.Application.Common.Models.Notifications;

/// <summary>
/// Non-secret settings for the Teknomart SMS provider (app.teknomart.com.tr).
/// Two endpoints are exposed: <c>/sms/create-otp</c> (OTP) and <c>/sms/create</c> (Single);
/// which one a given notification uses is decided per template, falling back to
/// <see cref="DefaultSmsKind"/>.
/// </summary>
public sealed class SmsTeknomartPublicSettings
{
    public bool IsEnabled { get; set; }
    public string? DisplayName { get; set; }

    /// <summary>Base URL including scheme and port, e.g. <c>https://api.teknomart.com.tr:9588</c>.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Approved sender name / originator.</summary>
    public string? Sender { get; set; }

    /// <summary>"Otp" or "Single" - used when a notification does not specify its own kind.</summary>
    public string DefaultSmsKind { get; set; } = "Single";

    /// <summary>Package title for Single sends (5-50 chars). Required only when Single is used.</summary>
    public string? SingleSmsTitle { get; set; }

    /// <summary>0 = default, 1 = Turkish, 2 = UTF-8.</summary>
    public int Encoding { get; set; }

    /// <summary>OTP validity 3-6; Single validity 60-1440 (minutes). 0 = provider default.</summary>
    public int Validity { get; set; }

    /// <summary>Marks sends as commercial so the provider runs the IYS check.</summary>
    public bool Commercial { get; set; }

    /// <summary>Optional webhook the provider POSTs delivery reports to.</summary>
    public string? PushWebhookUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>"Preserve" or "TransliterateToAscii".</summary>
    public string TurkishCharacterMode { get; set; } = "Preserve";
}

/// <summary>Secret settings for Teknomart: the Basic-auth credentials.</summary>
public sealed class SmsTeknomartSecretSettings
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}
