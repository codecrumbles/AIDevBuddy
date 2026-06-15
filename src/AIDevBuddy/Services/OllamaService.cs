using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AIDevBuddy.Services;

public class OllamaService : IOllamaService
{
    private static HttpClient MakeClient(string endpoint, int timeoutSeconds = 15)
    {
        Uri uri;
        try { uri = new Uri(endpoint.TrimEnd('/')); }
        catch { uri = new Uri("http://localhost:11434"); }

        return new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}"),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    public async Task<OllamaModelsResult> GetAvailableModelsAsync(string endpoint)
    {
        try
        {
            using var client = MakeClient(endpoint);
            var response = await client.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
            var models = response?.Models?
                .Select(m => m.Name)
                .OrderBy(n => n)
                .ToList() ?? new List<string>();

            return models.Any()
                ? new OllamaModelsResult(models, null)
                : new OllamaModelsResult(models,
                    $"Connected to {client.BaseAddress} but no models found. Run: ollama pull qwen3.5:2b");
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            return new OllamaModelsResult(new List<string>(),
                $"{ex.GetType().Name}: {detail}");
        }
    }

    public async Task<string> ChatAsync(string endpoint, string model, List<OllamaChatMessage> messages)
    {
        try
        {
            using var client = MakeClient(endpoint, timeoutSeconds: 300); // 5 min — LLMs can be slow

            var request = new
            {
                model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                stream = false
            };

            var response = await client.PostAsJsonAsync("/api/chat", request);
            var json = await response.Content.ReadAsStringAsync();

            var result = System.Text.Json.JsonSerializer.Deserialize<OllamaChatResponse>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            return $"[LLM error: {ex.InnerException?.Message ?? ex.Message}]";
        }
    }

    private record OllamaTagsResponse([property: JsonPropertyName("models")] List<OllamaModel>? Models);
    private record OllamaModel([property: JsonPropertyName("name")] string Name);
    private record OllamaChatResponse([property: JsonPropertyName("message")] OllamaChatMessageJson? Message);
    private record OllamaChatMessageJson([property: JsonPropertyName("content")] string Content);
}
