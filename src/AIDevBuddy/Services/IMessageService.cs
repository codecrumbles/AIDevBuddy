using AIDevBuddy.Data;

namespace AIDevBuddy.Services;

public interface IMessageService
{
    Task<List<AgentMessage>> GetAllMessagesAsync();
    Task<List<AgentMessage>> GetMessagesForAgentAsync(int agentId);
    Task<List<AgentMessage>> GetMessagesForProjectAsync(int projectId);
    Task<AgentMessage> SendMessageAsync(AgentMessage message);
    Task MarkAsReadAsync(int messageId);
    Task<int> GetUnreadCountAsync(int agentId);
}
