using AIDevBuddy.Data;

namespace AIDevBuddy.Services;

public interface IAgentService
{
    Task<List<Agent>> GetAllAgentsAsync();
    Task<Agent?> GetAgentByIdAsync(int id);
    Task<Agent> CreateAgentAsync(Agent agent);
    Task<Agent> UpdateAgentAsync(Agent agent);
    Task DeleteAgentAsync(int id);
    Task<Agent> UpdateAgentStatusAsync(int id, AgentStatus status);
}
