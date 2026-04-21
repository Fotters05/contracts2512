using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Contract2512.Models;

namespace Contract2512.Services;

public sealed class AiCourseDraftClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public AiCourseDraftClient(string? baseUrl = null)
    {
        var resolvedBaseUrl = baseUrl;
        if (string.IsNullOrWhiteSpace(resolvedBaseUrl))
        {
            resolvedBaseUrl = EnvConfigService.Get("AI_SERVICE_BASE_URL");
        }

        if (string.IsNullOrWhiteSpace(resolvedBaseUrl))
        {
            resolvedBaseUrl = "http://127.0.0.1:8010";
        }

        if (!resolvedBaseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            resolvedBaseUrl += "/";
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(resolvedBaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromMinutes(2),
        };
    }

    public Uri BaseAddress => _httpClient.BaseAddress!;

    public Task<AiHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
        => SendAsync<AiHealthResponse>(HttpMethod.Get, "api/health", null, cancellationToken);

    public Task<AiGenerateDraftResponse> GenerateDraftAsync(AiCourseSeedRequest request, CancellationToken cancellationToken = default)
        => SendAsync<AiGenerateDraftResponse>(HttpMethod.Post, "api/course-drafts/generate", request, cancellationToken);

    public Task<AiGenerateDraftResponse> GetDraftAsync(string draftId, CancellationToken cancellationToken = default)
        => SendAsync<AiGenerateDraftResponse>(HttpMethod.Get, $"api/course-drafts/{Uri.EscapeDataString(draftId)}", null, cancellationToken);

    public Task<AiDocumentExportResponse> ExportDocxAsync(string draftId, CancellationToken cancellationToken = default)
        => SendAsync<AiDocumentExportResponse>(HttpMethod.Post, $"api/course-drafts/{Uri.EscapeDataString(draftId)}/export-docx", null, cancellationToken);

    public string SerializeDraft(AiGenerateDraftResponse response)
    {
        return JsonSerializer.Serialize(response, _jsonOptions);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string relativeUrl, object? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUrl);
        if (payload != null)
        {
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = ExtractErrorMessage(raw);
            throw new InvalidOperationException(
                $"AI service returned {(int)response.StatusCode} ({response.ReasonPhrase}). {errorMessage}".Trim());
        }

        var result = JsonSerializer.Deserialize<T>(raw, _jsonOptions);
        if (result == null)
        {
            throw new InvalidOperationException("AI service returned an empty response.");
        }

        return result;
    }

    private static string ExtractErrorMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.ValueKind switch
                {
                    JsonValueKind.String => detail.GetString() ?? string.Empty,
                    _ => detail.ToString(),
                };
            }
        }
        catch
        {
            // Ignore JSON parse issues and fall back to raw response.
        }

        return raw;
    }
}
