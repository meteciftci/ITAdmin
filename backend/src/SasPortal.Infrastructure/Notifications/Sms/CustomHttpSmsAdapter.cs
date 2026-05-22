using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SasPortal.Application.Abstractions.Notifications;
using SasPortal.Application.Common.Constants;
using SasPortal.Application.Common.Models.Notifications;

namespace SasPortal.Infrastructure.Notifications.Sms;

public sealed class CustomHttpSmsAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<CustomHttpSmsAdapter> logger) : ISmsProviderAdapter
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "POST",
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/x-www-form-urlencoded",
        "text/xml",
        "text/plain",
    };

    private static readonly HashSet<string> AllowedAuthTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "None",
        "Basic",
        "BearerToken",
        "ApiKeyHeader",
        "ApiKeyQuery",
    };

    public string ProviderKey => NotificationProviderKeys.CustomHttp;
    public string DisplayName => "Custom HTTP";

    public SmsProviderDefinition GetDefinition() => new(ProviderKey, DisplayName);

    public Task<SmsSendResult> ValidateAsync(
        SmsProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateSettings(settings);
        return Task.FromResult(validationError is null
            ? new SmsSendResult(true, "SMS provider settings are valid.")
            : new SmsSendResult(false, validationError));
    }

    public async Task<SmsSendResult> SendAsync(
        SmsSendRequest request,
        SmsProviderRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateSettings(settings);
        if (validationError is not null)
        {
            return new SmsSendResult(false, validationError);
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return new SmsSendResult(false, "Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new SmsSendResult(false, "Message is required.");
        }

        var phone = NormalizePhone(request.PhoneNumber);
        var message = ApplyTurkishMode(request.Message, settings.Public.TurkishCharacterMode);

        try
        {
            using var httpRequest = BuildHttpRequest(phone, message, settings);
            var client = httpClientFactory.CreateClient("NotificationProviders");
            client.Timeout = TimeSpan.FromSeconds(settings.Public.TimeoutSeconds);

            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var successCodes = settings.Public.SuccessStatusCodes.Count == 0
                ? [200]
                : settings.Public.SuccessStatusCodes;

            if (!successCodes.Contains((int)response.StatusCode))
            {
                return new SmsSendResult(
                    false,
                    $"SMS provider returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                    SanitizeProviderSummary(response.StatusCode, responseBody));
            }

            if (!string.IsNullOrWhiteSpace(settings.Public.SuccessBodyContains)
                && !responseBody.Contains(settings.Public.SuccessBodyContains, StringComparison.OrdinalIgnoreCase))
            {
                return new SmsSendResult(
                    false,
                    "SMS provider response did not match success criteria.",
                    SanitizeProviderSummary(response.StatusCode, responseBody));
            }

            return new SmsSendResult(
                true,
                "SMS sent successfully.",
                SanitizeProviderSummary(response.StatusCode, responseBody));
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Custom HTTP SMS request timed out.");
            return new SmsSendResult(false, "SMS provider request timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Custom HTTP SMS request failed.");
            return new SmsSendResult(false, "SMS provider request failed. Check endpoint and network connectivity.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Custom HTTP SMS request failed unexpectedly.");
            return new SmsSendResult(false, "SMS provider request failed.");
        }
    }

    internal static string? ValidateSettings(SmsProviderRuntimeSettings settings)
    {
        var publicSettings = settings.Public;

        if (string.IsNullOrWhiteSpace(publicSettings.EndpointUrl)
            || !Uri.TryCreate(publicSettings.EndpointUrl, UriKind.Absolute, out _))
        {
            return "Endpoint URL must be a valid absolute URL.";
        }

        if (!AllowedMethods.Contains(publicSettings.Method))
        {
            return "HTTP method is invalid.";
        }

        if (!AllowedContentTypes.Contains(publicSettings.ContentType))
        {
            return "Content type is invalid.";
        }

        if (!AllowedAuthTypes.Contains(publicSettings.AuthType))
        {
            return "Authentication type is invalid.";
        }

        if (publicSettings.TimeoutSeconds is < 5 or > 300)
        {
            return "Timeout must be between 5 and 300 seconds.";
        }

        return ValidateAuth(settings);
    }

    private static string? ValidateAuth(SmsProviderRuntimeSettings settings)
    {
        var authType = settings.Public.AuthType;
        var secrets = settings.Secrets;

        return authType switch
        {
            "Basic" when string.IsNullOrWhiteSpace(secrets.BasicUserName)
                || string.IsNullOrWhiteSpace(secrets.BasicPassword)
                => "Basic authentication requires username and password.",
            "BearerToken" when string.IsNullOrWhiteSpace(secrets.BearerToken)
                => "Bearer token authentication requires a token.",
            "ApiKeyHeader" or "ApiKeyQuery"
                when string.IsNullOrWhiteSpace(settings.Public.ApiKeyName)
                    || string.IsNullOrWhiteSpace(secrets.ApiKeyValue)
                => "API key authentication requires key name and value.",
            _ => null,
        };
    }

    private HttpRequestMessage BuildHttpRequest(
        string phone,
        string message,
        SmsProviderRuntimeSettings settings)
    {
        var publicSettings = settings.Public;
        var secrets = settings.Secrets;
        var endpoint = publicSettings.EndpointUrl!.Trim();
        var method = publicSettings.Method.ToUpperInvariant();

        var queryPairs = BuildPairs(publicSettings.QueryParameters, phone, message, settings);
        ApplyAuthQuery(publicSettings, secrets, queryPairs);

        if (string.Equals(method, "GET", StringComparison.Ordinal))
        {
            var uriBuilder = new UriBuilder(endpoint);
            var existingQuery = uriBuilder.Query.TrimStart('?');
            var combinedQuery = CombineQuery(existingQuery, queryPairs);
            if (!string.IsNullOrWhiteSpace(combinedQuery))
            {
                uriBuilder.Query = combinedQuery;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            ApplyHeaders(request, publicSettings, secrets, phone, message, settings);
            return request;
        }

        var postRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        ApplyHeaders(postRequest, publicSettings, secrets, phone, message, settings);

        var body = NotificationTemplateReplacer.Apply(
            publicSettings.BodyTemplate,
            phone,
            message,
            publicSettings,
            secrets);

        if (!string.IsNullOrWhiteSpace(body))
        {
            postRequest.Content = BuildContent(body, publicSettings.ContentType);
        }

        if (queryPairs.Count > 0)
        {
            var uriBuilder = new UriBuilder(endpoint);
            var existingQuery = uriBuilder.Query.TrimStart('?');
            uriBuilder.Query = CombineQuery(existingQuery, queryPairs);
            postRequest.RequestUri = uriBuilder.Uri;
        }

        return postRequest;
    }

    private static void ApplyHeaders(
        HttpRequestMessage request,
        SmsCustomHttpPublicSettings publicSettings,
        SmsCustomHttpSecretSettings secrets,
        string phone,
        string message,
        SmsProviderRuntimeSettings settings)
    {
        foreach (var pair in publicSettings.Headers)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var value = NotificationTemplateReplacer.Apply(pair.Value, phone, message, publicSettings, secrets);
            request.Headers.TryAddWithoutValidation(pair.Key.Trim(), value);
        }

        switch (publicSettings.AuthType)
        {
            case "Basic":
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{secrets.BasicUserName}:{secrets.BasicPassword}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;
            case "BearerToken":
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secrets.BearerToken);
                break;
            case "ApiKeyHeader" when !string.IsNullOrWhiteSpace(publicSettings.ApiKeyName):
                request.Headers.TryAddWithoutValidation(
                    publicSettings.ApiKeyName.Trim(),
                    secrets.ApiKeyValue ?? string.Empty);
                break;
        }
    }

    private static void ApplyAuthQuery(
        SmsCustomHttpPublicSettings publicSettings,
        SmsCustomHttpSecretSettings secrets,
        List<KeyValuePair<string, string>> queryPairs)
    {
        if (string.Equals(publicSettings.AuthType, "ApiKeyQuery", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(publicSettings.ApiKeyName)
            && !string.IsNullOrWhiteSpace(secrets.ApiKeyValue))
        {
            queryPairs.Add(new KeyValuePair<string, string>(
                publicSettings.ApiKeyName.Trim(),
                secrets.ApiKeyValue));
        }
    }

    private static List<KeyValuePair<string, string>> BuildPairs(
        IReadOnlyList<NotificationKeyValuePair> source,
        string phone,
        string message,
        SmsProviderRuntimeSettings settings)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var item in source)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            var value = NotificationTemplateReplacer.Apply(
                item.Value,
                phone,
                message,
                settings.Public,
                settings.Secrets);
            pairs.Add(new KeyValuePair<string, string>(item.Key.Trim(), value));
        }

        return pairs;
    }

    private static HttpContent? BuildContent(string body, string contentType)
    {
        var mediaType = contentType.Trim().ToLowerInvariant();
        return mediaType switch
        {
            "application/json" => new StringContent(body, Encoding.UTF8, "application/json"),
            "application/x-www-form-urlencoded" => new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"),
            "text/xml" => new StringContent(body, Encoding.UTF8, "text/xml"),
            _ => new StringContent(body, Encoding.UTF8, "text/plain"),
        };
    }

    private static string CombineQuery(string? existingQuery, IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(existingQuery))
        {
            segments.Add(existingQuery);
        }

        foreach (var pair in pairs)
        {
            segments.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        }

        return string.Join('&', segments);
    }

    private static string NormalizePhone(string phoneNumber) => phoneNumber.Trim();

    private static string ApplyTurkishMode(string message, string? mode)
    {
        if (string.Equals(mode, "TransliterateToAscii", StringComparison.OrdinalIgnoreCase))
        {
            return TurkishCharacterTransliterator.Transliterate(message);
        }

        return message;
    }

    private static string? SanitizeProviderSummary(HttpStatusCode statusCode, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"HTTP {(int)statusCode}";
        }

        var trimmed = body.Trim();
        return trimmed.Length <= 200 ? $"HTTP {(int)statusCode}: {trimmed}" : $"HTTP {(int)statusCode}: {trimmed[..200]}...";
    }
}
