using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIDevBuddy.Services;

/// <summary>
/// GitHub Copilot integration via OAuth device flow.
///
/// Flow:
///  1. RequestDeviceCodeAsync()   → user visits github.com/login/device with the user_code
///  2. PollForOAuthTokenAsync()   → polls until the user approves; returns GitHub OAuth token
///  3. ChatAsync(oauthToken, …)   → exchanges for a short-lived Copilot session token,
///                                   caches it, then calls api.githubcopilot.com
///
/// The OAuth token is stored in LlmSetting.ApiKey (persisted in DB).
/// The Copilot session token (~30 min TTL) is cached in memory here.
/// </summary>
public sealed class CopilotService : ICopilotService
{
    // Public GitHub OAuth app used by the official VS Code Copilot extension.
    private const string ClientId       = "Iv1.b507a08c87ecfe98";
    private const string CopilotBaseUrl = "https://api.githubcopilot.com";
    private const string GitHubApiUrl   = "https://api.github.com";

    private readonly ILogger<CopilotService> _logger;

    public string? LastError { get; private set; }

    // In-memory Copilot session token cache (per OAuth token)
    private string? _cachedCopilotToken;
    private DateTime _copilotTokenExpiresAt = DateTime.MinValue;
    private string? _cachedOAuthToken; // the OAuth token the cached Copilot token was derived from

    public CopilotService(ILogger<CopilotService> logger)
    {
        _logger = logger;
    }

    // ── Step 1: Request device code ──────────────────────────────────────────

    public async Task<CopilotDeviceCodeResponse?> RequestDeviceCodeAsync()
    {
        LastError = null;
        try
        {
            using var client = MakeGitHubOAuthClient();
            var response = await client.PostAsJsonAsync(
                "https://github.com/login/device/code",
                new { client_id = ClientId, scope = "read:user" });

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Device code response ({Status}): {Body}", response.StatusCode, body);

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"HTTP {(int)response.StatusCode}: {TruncateError(body)}";
                _logger.LogWarning("RequestDeviceCodeAsync failed: {Error}", LastError);
                return null;
            }

            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("device_code",      out var deviceCodeEl) ||
                !root.TryGetProperty("user_code",        out var userCodeEl) ||
                !root.TryGetProperty("verification_uri", out var uriEl))
            {
                LastError = $"Unexpected response format: {TruncateError(body)}";
                _logger.LogWarning("RequestDeviceCodeAsync: missing fields. Body: {Body}", body);
                return null;
            }

            var expiresIn = root.TryGetProperty("expires_in", out var expEl) && expEl.TryGetInt32(out var exp) ? exp : 900;
            var interval  = root.TryGetProperty("interval",   out var ivEl)  && ivEl.TryGetInt32(out var iv)  ? iv  : 5;

            return new CopilotDeviceCodeResponse(
                deviceCodeEl.GetString()!,
                userCodeEl.GetString()!,
                uriEl.GetString()!,
                expiresIn,
                interval);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "RequestDeviceCodeAsync failed");
            return null;
        }
    }

    // ── Step 2: Poll for OAuth token ─────────────────────────────────────────

    public async Task<string?> PollForOAuthTokenAsync(
        string deviceCode, int intervalSeconds, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(intervalSeconds, 5));

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(delay, ct);
            try
            {
                using var client = MakeGitHubOAuthClient();
                var response = await client.PostAsJsonAsync(
                    "https://github.com/login/oauth/access_token",
                    new
                    {
                        client_id   = ClientId,
                        device_code = deviceCode,
                        grant_type  = "urn:ietf:params:oauth:grant-type:device_code"
                    }, ct);

                var body = await response.Content.ReadAsStringAsync(ct);
                var doc  = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("access_token", out var tokenEl))
                {
                    var token = tokenEl.GetString();
                    if (!string.IsNullOrWhiteSpace(token)) return token;
                }

                if (root.TryGetProperty("error", out var errEl))
                {
                    var err = errEl.GetString();
                    if (err == "authorization_pending") continue;
                    if (err == "slow_down") { delay += TimeSpan.FromSeconds(5); continue; }
                    var desc = root.TryGetProperty("error_description", out var descEl) ? descEl.GetString() : err;
                    LastError = $"OAuth error: {desc}";
                    _logger.LogWarning("OAuth poll error: {Err}", LastError);
                    return null;
                }
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _logger.LogError(ex, "PollForOAuthTokenAsync error");
                return null;
            }
        }
        return null;
    }

    // ── Auth check ───────────────────────────────────────────────────────────

    public async Task<bool> IsAuthenticatedAsync(string oauthToken)
    {
        if (string.IsNullOrWhiteSpace(oauthToken)) return false;
        var ct = await GetOrRefreshCopilotTokenAsync(oauthToken);
        return !string.IsNullOrWhiteSpace(ct);
    }

    // ── Model list ───────────────────────────────────────────────────────────

    public async Task<List<string>> GetModelsAsync(string oauthToken)
    {
        try
        {
            var copilotToken = await GetOrRefreshCopilotTokenAsync(oauthToken);
            if (string.IsNullOrWhiteSpace(copilotToken)) return new();

            using var client = MakeCopilotClient(copilotToken);
            var response = await client.GetAsync("/models");
            response.EnsureSuccessStatusCode();

            var json    = await response.Content.ReadAsStringAsync();
            var doc     = JsonDocument.Parse(json);
            var models  = new List<string>();

            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id))
                    {
                        // Only include chat-capable models
                        var isChatModel = true;
                        if (item.TryGetProperty("capabilities", out var caps) &&
                            caps.TryGetProperty("type", out var capType))
                            isChatModel = capType.GetString() != "embeddings";

                        if (isChatModel)
                            models.Add(id.GetString() ?? "");
                    }
                }
            }

            return models.Where(m => !string.IsNullOrWhiteSpace(m)).OrderBy(m => m).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetModelsAsync failed");
            LastError = ex.Message;
            return new();
        }
    }

    // ── Chat completions ─────────────────────────────────────────────────────

    public async Task<string> ChatAsync(
        string oauthToken, string model, List<OllamaChatMessage> messages)
    {
        try
        {
            var copilotToken = await GetOrRefreshCopilotTokenAsync(oauthToken);
            if (string.IsNullOrWhiteSpace(copilotToken))
                return "[LLM error: Copilot not authenticated — connect via Settings → LLM Connections]";

            using var client = MakeCopilotClient(copilotToken);
            client.Timeout = TimeSpan.FromMinutes(5);

            var payload = new
            {
                model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                stream   = false
            };

            var response = await client.PostAsJsonAsync("/chat/completions", payload);
            var body     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Copilot API {Status}: {Body}", response.StatusCode, body);
                return $"[LLM error: Copilot returned {(int)response.StatusCode} — {TruncateError(body)}]";
            }

            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
                return content.GetString() ?? string.Empty;

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CopilotService.ChatAsync failed");
            return $"[LLM error: {ex.Message}]";
        }
    }

    // ── Copilot session token (internal) ─────────────────────────────────────

    private async Task<string?> GetOrRefreshCopilotTokenAsync(string oauthToken)
    {
        if (string.IsNullOrWhiteSpace(oauthToken)) return null;

        // Return cached token if still valid (with 60s buffer)
        if (_cachedOAuthToken == oauthToken &&
            !string.IsNullOrWhiteSpace(_cachedCopilotToken) &&
            DateTime.UtcNow < _copilotTokenExpiresAt.AddSeconds(-60))
            return _cachedCopilotToken;

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", oauthToken);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "AIDevBuddy/1.0");

            var response = await client.GetAsync(
                $"{GitHubApiUrl}/copilot_internal/v2/token");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Copilot token refresh failed: {Status}", response.StatusCode);
                _cachedCopilotToken = null;
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("token", out var tokenEl)) return null;
            var token = tokenEl.GetString();

            // Parse expiry ("expires_at": unix timestamp)
            var expiresAt = DateTime.UtcNow.AddMinutes(25); // safe default
            if (doc.RootElement.TryGetProperty("expires_at", out var expEl))
            {
                if (expEl.TryGetInt64(out var unixTs))
                    expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime;
            }

            _cachedCopilotToken  = token;
            _copilotTokenExpiresAt = expiresAt;
            _cachedOAuthToken    = oauthToken;
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOrRefreshCopilotTokenAsync failed");
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // GitHub OAuth endpoints return JSON when Accept: application/json is sent.
    private static HttpClient MakeGitHubOAuthClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.Add("Accept", "application/json");
        c.DefaultRequestHeaders.Add("User-Agent", "AIDevBuddy/1.0");
        c.Timeout = TimeSpan.FromSeconds(30);
        return c;
    }

    private static HttpClient MakeCopilotClient(string copilotToken)
    {
        var c = new HttpClient { BaseAddress = new Uri(CopilotBaseUrl) };
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", copilotToken);
        c.DefaultRequestHeaders.Add("Accept", "application/json");
        c.DefaultRequestHeaders.Add("User-Agent", "AIDevBuddy/1.0");
        c.DefaultRequestHeaders.Add("Copilot-Integration-Id", "vscode-chat");
        c.DefaultRequestHeaders.Add("Editor-Version", "vscode/1.85.0");
        c.DefaultRequestHeaders.Add("Editor-Plugin-Version", "copilot-chat/0.11.1");
        return c;
    }

    private static string TruncateError(string s) =>
        s.Length > 200 ? s[..200] + "…" : s;
}
