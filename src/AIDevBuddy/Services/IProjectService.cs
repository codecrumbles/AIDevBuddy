using AIDevBuddy.Data;

namespace AIDevBuddy.Services;

public interface IProjectService
{
    Task<List<Project>> GetAllProjectsAsync();
    Task<Project?> GetProjectByIdAsync(int id);
    Task<Project> CreateProjectAsync(Project project, List<int>? agentIds = null);
    Task<Project> UpdateProjectAsync(Project project);
    Task DeleteProjectAsync(int id);
    Task AssignAgentAsync(int projectId, int agentId, string? role = null);
    Task RemoveAgentAsync(int projectId, int agentId);
    Task<Project> StartProjectAsync(int projectId);
    Task<AgentMessage> SendPmMessageAsync(int projectId, string userMessage);
}
