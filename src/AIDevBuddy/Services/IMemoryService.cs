using AIDevBuddy.Data;

namespace AIDevBuddy.Services;

public interface IMemoryService
{
    Task<List<BmadMemory>> GetAllMemoriesAsync();
    Task<List<BmadMemory>> GetMemoriesForProjectAsync(int projectId);
    Task<BmadMemory> UpsertMemoryAsync(string key, string value, int? projectId, string? agentName, string category = "General");
    Task DeleteMemoryAsync(int id);
}
