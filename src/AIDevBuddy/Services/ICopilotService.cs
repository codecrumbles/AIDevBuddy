namespace AIDevBuddy.Services;

public record CopilotDeviceCodeResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int ExpiresIn,
    int Interval);

public interface ICopilotService
{
    /// <summary>Step 1 — request a device code from GitHub. Returns null and sets lastError on failure.</summary>
    Task<CopilotDeviceCodeResponse?> RequestDeviceCodeAsync();

    /// <summary>Last error message from RequestDeviceCodeAsync or PollForOAuthTokenAsync.</summary>
    string? LastError { get; }

    /// <summary>
    /// Step 2 — poll until the user completes auth at github.com/login/device.
    /// Returns the GitHub OAuth access token, or null on failure/timeout.
    /// </summary>
    Task<string?> PollForOAuthTokenAsync(string deviceCode, int intervalSeconds, CancellationToken ct);

    /// <summary>True if an OAuth token is stored and a Copilot session token can be obtained.</summary>
    Task<bool> IsAuthenticatedAsync(string oauthToken);

    /// <summary>List models available to this Copilot subscription.</summary>
    Task<List<string>> GetModelsAsync(string oauthToken);

    /// <summary>Chat completions via the Copilot API (OpenAI-compatible format).</summary>
    Task<string> ChatAsync(string oauthToken, string model, List<OllamaChatMessage> messages);
}
