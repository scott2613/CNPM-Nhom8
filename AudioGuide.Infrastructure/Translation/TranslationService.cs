using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AudioGuide.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AudioGuide.Infrastructure.Translation;

public sealed class TranslationService(HttpClient client, IConfiguration configuration) : ITranslationService
{
    public async Task<string> TranslateAsync(string text, string targetLanguage, string sourceLanguage = "vi",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return (await TranslateBatchAsync([text], targetLanguage, sourceLanguage, cancellationToken))[0];
    }

    public async Task<(string Title, string Description)> TranslatePoiContentAsync(string title, string description,
        string targetLanguage, CancellationToken cancellationToken = default)
    {
        string[] inputs = string.IsNullOrWhiteSpace(description) ? [title] : [title, description];
        var translated = await TranslateBatchAsync(inputs, targetLanguage, "vi", cancellationToken);
        return (translated[0], translated.Length == 2 ? translated[1] : "");
    }

    private async Task<string[]> TranslateBatchAsync(string[] inputs, string target, string source,
        CancellationToken cancellationToken)
    {
        var key = configuration["Translation:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Translation provider is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://translation.googleapis.com/language/translate/v2");
        request.Headers.Add("X-Goog-Api-Key", key.Trim());
        request.Content = JsonContent.Create(new { q = inputs, target, source, format = "text" });
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var entries = json.RootElement.GetProperty("data").GetProperty("translations");
            if (entries.GetArrayLength() != inputs.Length)
                throw new HttpRequestException("Translation provider returned an unexpected number of results.");
            var results = entries.EnumerateArray()
                .Select(entry => WebUtility.HtmlDecode(entry.GetProperty("translatedText").GetString())?.Trim())
                .ToArray();
            if (results.Any(string.IsNullOrWhiteSpace))
                throw new HttpRequestException("Translation provider returned empty content.");
            return results.Select(result => result!).ToArray();
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new HttpRequestException("Translation provider returned an invalid response.", error);
        }
    }
}
