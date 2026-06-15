using AIDevBuddy.Data;

namespace AIDevBuddy.Services;

/// <summary>
/// Routes LLM chat calls to the active provider (default → first enabled).
/// Supports: GitHub Copilot (OAuth) and Local LLM (Ollama).
/// Registered as scoped — use IServiceScopeFactory in singletons.
/// </summary>
public sealed class LlmChatService : ILlmChatService
{
    private readonly ILlmSettingsService _settings;
    private readonly IOllamaService      _ollama;
    private readonly ICopilotService     _copilot;

    public LlmChatService(
        ILlmSettingsService settings,
        IOllamaService      ollama,
        ICopilotService     copilot)
    {
        _settings = settings;
        _ollama   = ollama;
        _copilot  = copilot;
    }

    public async Task<string> ChatAsync(List<OllamaChatMessage> messages, Agent? agent = null)
    {
        var active = await GetActiveSettingAsync();
        if (active == null)
            return "[LLM error: no LLM provider is enabled. Enable one in Settings → LLM Connections.]";

        return active.Provider switch
        {
            LlmProvider.Copilot =>
                await _copilot.ChatAsync(
                    active.ApiKey ?? "",
                    agent?.PreferredModel ?? active.ModelName ?? "gpt-4o",
                    messages),

            LlmProvider.LocalLlm =>
                await _ollama.ChatAsync(
                    active.Endpoint ?? "http://localhost:11434",
                    agent?.PreferredModel ?? active.ModelName ?? "llama3",
                    messages),

            // GLM and Claude use OpenAI-compatible chat endpoints
            LlmProvider.GLM or LlmProvider.Claude =>
                await CallOpenAiCompatibleAsync(active, agent, messages),

            _ => $"[LLM error: provider {active.Provider} is not yet supported]"
        };
    }

    public async Task<bool> IsAvailableAsync()
        => await GetActiveSettingAsync() != null;

    public async Task<string> GetActiveProviderLabelAsync()
    {
        var s = await GetActiveSettingAsync();
        if (s == null) return "None";
        return s.Provider switch
        {
            LlmProvider.Copilot  => $"GitHub Copilot ({s.ModelName ?? "gpt-4o"})",
            LlmProvider.LocalLlm => $"Ollama ({s.ModelName ?? "llama3"})",
            LlmProvider.GLM      => $"GLM ({s.ModelName ?? "glm-4"})",
            LlmProvider.Claude   => $"Claude ({s.ModelName ?? "claude-sonnet-4-6"})",
            _                    => s.Provider.ToString()
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<LlmSetting?> GetActiveSettingAsync()
    {
        var all = await _settings.GetAllSettingsAsync();
        return all.FirstOrDefault(s => s.IsDefault && s.IsEnabled)
               ?? all.FirstOrDefault(s => s.IsEnabled);
    }

    /// <summary>GLM / Claude both speak the OpenAI chat completions format.</summary>
    private async Task<string> CallOpenAiCompatibleAsync(
        LlmSetting setting, Agent? agent, List<OllamaChatMessage> messages)
    {
        try
        {
            var endpoint = (setting.Endpoint ?? "").TrimEnd('/');
            var model    = agent?.PreferredModel ?? setting.ModelName ?? "gpt-4";

            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {setting.ApiKey}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromMinutes(5);

            var payload = new
            {
                model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                stream   = false
            };

            var response = await client.PostAsJsonAsync($"{endpoint}/chat/completions", payload);
            var body     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"[LLM error: {(int)response.StatusCode} — {body[..Math.Min(200, body.Length)]}]";

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
                return content.GetString() ?? string.Empty;

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"[LLM error: {ex.Message}]";
        }
    }
}
