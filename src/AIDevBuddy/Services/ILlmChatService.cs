using AIDevBuddy.Data;

namespace AIDevBuddy.Services;

public interface ILlmChatService
{
    /// <summary>
    /// Send a chat to whichever LLM provider is currently active (default/enabled).
    /// Pass the agent so the router can respect PreferredModel.
    /// </summary>
    Task<string> ChatAsync(List<OllamaChatMessage> messages, Agent? agent = null);

    /// <summary>True when at least one provider is enabled and reachable.</summary>
    Task<bool> IsAvailableAsync();

    /// <summary>Human-readable name of the active provider, e.g. "GitHub Copilot (gpt-4o)".</summary>
    Task<string> GetActiveProviderLabelAsync();
}
