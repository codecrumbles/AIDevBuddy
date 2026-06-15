namespace AIDevBuddy.Services;

public record OllamaChatMessage(string Role, string Content);
public record OllamaModelsResult(List<string> Models, string? Error);

public interface IOllamaService
{
    Task<OllamaModelsResult> GetAvailableModelsAsync(string endpoint);
    Task<string> ChatAsync(string endpoint, string model, List<OllamaChatMessage> messages);
}
