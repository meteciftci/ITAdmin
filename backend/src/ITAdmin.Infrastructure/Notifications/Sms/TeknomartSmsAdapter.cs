using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Notifications;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models.Notifications;

namespace ITAdmin.Infrastructure.Notifications.Sms;

/// <summary>
/// Teknomart SMS (app.teknomart.com.tr). Basic auth over HTTPS; two endpoints:
/// <c>POST {baseUrl}/sms/create-otp</c> for OTP and <c>POST {baseUrl}/sms/create</c> for Single.
/// The kind is chosen per notification (<see cref="SmsSendRequest.Kind"/>), falling back to the
/// configured default.
/// </summary>
public sealed class TeknomartSmsAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<TeknomartSmsAdapter> logger) : ISmsProviderAdapter
{
    private const string OtpPath = "/sms/create-otp";
    private const string SinglePath = "/sms/create";

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ProviderKey => NotificationProviderKeys.Teknomart;
    public string DisplayName => "Teknomart SMS";

    public SmsProviderDefinition GetDefinition() => new(ProviderKey, DisplayName);

    public Task<SmsSendResult> ValidateAsync(
        SmsProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        var (publicSettings, secrets) = Parse(settings);
        var error = ValidateSettings(publicSettings, secrets, resolvedKind: null);
        return Task.FromResult(error is null
            ? new SmsSendResult(true, "Teknomart SMS settings are valid.")
            : new SmsSendResult(false, error));
    }

    public async Task<SmsSendResult> SendAsync(
        SmsSendRequest request,
        SmsProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        var (publicSettings, secrets) = Parse(settings);
        var kind = ResolveKind(request.Kind, publicSettings.DefaultSmsKind);

        var error = ValidateSettings(publicSettings, secrets, kind);
        if (error is not null)
        {
            return new SmsSendResult(false, error);
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return new SmsSendResult(false, "Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new SmsSendResult(false, "Message is required.");
        }

        var content = ApplyTurkishMode(request.Message, publicSettings.TurkishCharacterMode);
        var path = kind == SmsSendKind.Otp ? OtpPath : SinglePath;
        var url = $"{publicSettings.BaseUrl!.TrimEnd('/')}{path}";
        var body = BuildBody(kind, request.PhoneNumber.Trim(), content, publicSettings);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body, JsonWriteOptions), Encoding.UTF8, "application/json"),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{secrets.Username}:{secrets.Password}")));

            var client = httpClientFactory.CreateClient("NotificationProviders");
            client.Timeout = TimeSpan.FromSeconds(
                publicSettings.TimeoutSeconds is >= 5 and <= 300 ? publicSettings.TimeoutSeconds : 30);

            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            return Interpret(kind, response.StatusCode, responseBody);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Teknomart SMS request timed out.");
            return new SmsSendResult(false, "Teknomart SMS request timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Teknomart SMS request failed.");
            return new SmsSendResult(false, "Teknomart SMS request failed. Check the base URL and network connectivity.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Teknomart SMS request failed unexpectedly.");
            return new SmsSendResult(false, "Teknomart SMS request failed.");
        }
    }

    internal static SmsSendKind ResolveKind(SmsSendKind requested, string? configuredDefault)
    {
        if (requested is SmsSendKind.Otp or SmsSendKind.Single)
        {
            return requested;
        }

        return string.Equals(configuredDefault, "Otp", StringComparison.OrdinalIgnoreCase)
            ? SmsSendKind.Otp
            : SmsSendKind.Single;
    }

    internal static string? ValidateSettings(
        SmsTeknomartPublicSettings publicSettings,
        SmsTeknomartSecretSettings secrets,
        SmsSendKind? resolvedKind)
    {
        if (string.IsNullOrWhiteSpace(publicSettings.BaseUrl)
            || !Uri.TryCreate(publicSettings.BaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "Base URL must be a valid absolute http(s) URL (e.g. https://api.teknomart.com.tr:9588).";
        }

        if (string.IsNullOrWhiteSpace(secrets.Username) || string.IsNullOrWhiteSpace(secrets.Password))
        {
            return "Teknomart requires an API username and password.";
        }

        if (string.IsNullOrWhiteSpace(publicSettings.Sender))
        {
            return "A sender name is required.";
        }

        if (publicSettings.Encoding is < 0 or > 2)
        {
            return "Encoding must be 0 (default), 1 (Turkish) or 2 (UTF-8).";
        }

        if (!string.Equals(publicSettings.DefaultSmsKind, "Otp", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(publicSettings.DefaultSmsKind, "Single", StringComparison.OrdinalIgnoreCase))
        {
            return "Default SMS kind must be 'Otp' or 'Single'.";
        }

        // Single needs a package title. Enforce it when Single is the effective kind, or when we are
        // only validating settings (resolvedKind null) and Single is reachable as the default.
        var singleReachable = resolvedKind == SmsSendKind.Single
            || (resolvedKind is null
                && !string.Equals(publicSettings.DefaultSmsKind, "Otp", StringComparison.OrdinalIgnoreCase));
        if (singleReachable)
        {
            var title = publicSettings.SingleSmsTitle?.Trim();
            if (string.IsNullOrEmpty(title) || title.Length is < 5 or > 50)
            {
                return "Single SMS title is required and must be 5-50 characters.";
            }
        }

        return null;
    }

    private static (SmsTeknomartPublicSettings Public, SmsTeknomartSecretSettings Secrets) Parse(
        SmsProviderRuntimeSettings settings)
    {
        var publicSettings = Deserialize<SmsTeknomartPublicSettings>(settings.PublicJson) ?? new SmsTeknomartPublicSettings();
        var secrets = Deserialize<SmsTeknomartSecretSettings>(settings.SecretJson) ?? new SmsTeknomartSecretSettings();
        return (publicSettings, secrets);
    }

    private static T? Deserialize<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json) || json == "{}"
            ? null
            : JsonSerializer.Deserialize<T>(json, JsonReadOptions);

    private static object BuildBody(
        SmsSendKind kind,
        string number,
        string content,
        SmsTeknomartPublicSettings settings)
    {
        int? encoding = settings.Encoding is > 0 and <= 2 ? settings.Encoding : null;
        var push = string.IsNullOrWhiteSpace(settings.PushWebhookUrl)
            ? null
            : new { url = settings.PushWebhookUrl!.Trim() };
        var commercial = settings.Commercial ? (bool?)true : null;

        if (kind == SmsSendKind.Otp)
        {
            int? validity = settings.Validity is >= 3 and <= 6 ? settings.Validity : null;
            return new TeknomartOtpBody
            {
                Number = number,
                Sender = settings.Sender!.Trim(),
                Content = content,
                Encoding = encoding,
                Validity = validity,
                Commercial = commercial,
                PushSettings = push,
            };
        }

        int? singleValidity = settings.Validity is >= 60 and <= 1440 ? settings.Validity : null;
        return new TeknomartSingleBody
        {
            Type = 1,
            SendingType = 0,
            Number = number,
            Sender = settings.Sender!.Trim(),
            Title = settings.SingleSmsTitle!.Trim(),
            Content = content,
            Encoding = encoding,
            Validity = singleValidity,
            Commercial = commercial,
            PushSettings = push,
        };
    }

    private SmsSendResult Interpret(SmsSendKind kind, System.Net.HttpStatusCode statusCode, string? responseBody)
    {
        TeknomartResponse? parsed = null;
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<TeknomartResponse>(responseBody, JsonReadOptions);
            }
            catch (JsonException)
            {
                // fall through to status-code handling
            }
        }

        if (parsed?.Err is not null)
        {
            var code = string.IsNullOrWhiteSpace(parsed.Err.Code) ? "" : $" [{parsed.Err.Code}]";
            var message = string.IsNullOrWhiteSpace(parsed.Err.Message) ? "The provider rejected the request." : parsed.Err.Message!;
            return new SmsSendResult(false, $"Teknomart: {message}{code}", $"HTTP {(int)statusCode}");
        }

        if (!IsSuccessStatus(statusCode))
        {
            return new SmsSendResult(
                false,
                $"Teknomart returned HTTP {(int)statusCode}.",
                Truncate(responseBody, (int)statusCode));
        }

        var pkg = parsed?.Data?.PkgId;
        return new SmsSendResult(
            true,
            $"SMS sent via Teknomart ({(kind == SmsSendKind.Otp ? "OTP" : "Single")}).",
            pkg is not null ? $"pkgID {pkg}" : $"HTTP {(int)statusCode}");
    }

    private static bool IsSuccessStatus(System.Net.HttpStatusCode statusCode) =>
        (int)statusCode is >= 200 and < 300;

    private static string? Truncate(string? body, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"HTTP {statusCode}";
        }

        var trimmed = body.Trim();
        return trimmed.Length <= 200 ? $"HTTP {statusCode}: {trimmed}" : $"HTTP {statusCode}: {trimmed[..200]}...";
    }

    private static string ApplyTurkishMode(string message, string? mode) =>
        string.Equals(mode, "TransliterateToAscii", StringComparison.OrdinalIgnoreCase)
            ? TurkishCharacterTransliterator.Transliterate(message)
            : message;

    private sealed class TeknomartOtpBody
    {
        [JsonPropertyName("number")] public string Number { get; init; } = string.Empty;
        [JsonPropertyName("sender")] public string Sender { get; init; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
        [JsonPropertyName("encoding")] public int? Encoding { get; init; }
        [JsonPropertyName("validity")] public int? Validity { get; init; }
        [JsonPropertyName("commercial")] public bool? Commercial { get; init; }
        [JsonPropertyName("pushSettings")] public object? PushSettings { get; init; }
    }

    private sealed class TeknomartSingleBody
    {
        [JsonPropertyName("type")] public int Type { get; init; }
        [JsonPropertyName("sendingType")] public int SendingType { get; init; }
        [JsonPropertyName("number")] public string Number { get; init; } = string.Empty;
        [JsonPropertyName("sender")] public string Sender { get; init; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
        [JsonPropertyName("encoding")] public int? Encoding { get; init; }
        [JsonPropertyName("validity")] public int? Validity { get; init; }
        [JsonPropertyName("commercial")] public bool? Commercial { get; init; }
        [JsonPropertyName("pushSettings")] public object? PushSettings { get; init; }
    }

    private sealed class TeknomartResponse
    {
        [JsonPropertyName("data")] public TeknomartData? Data { get; init; }
        [JsonPropertyName("err")] public TeknomartError? Err { get; init; }
    }

    private sealed class TeknomartData
    {
        [JsonPropertyName("pkgID")] public long? PkgId { get; init; }
    }

    private sealed class TeknomartError
    {
        [JsonPropertyName("code")] public string? Code { get; init; }
        [JsonPropertyName("status")] public int Status { get; init; }
        [JsonPropertyName("message")] public string? Message { get; init; }
    }
}
