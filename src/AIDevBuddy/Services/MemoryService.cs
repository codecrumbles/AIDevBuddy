using AIDevBuddy.Data;
using Microsoft.EntityFrameworkCore;

namespace AIDevBuddy.Services;

public class MemoryService : IMemoryService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public MemoryService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<BmadMemory>> GetAllMemoriesAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.BmadMemories
            .Include(m => m.Project)
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<BmadMemory>> GetMemoriesForProjectAsync(int projectId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.BmadMemories
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.Category).ThenBy(m => m.Key)
            .ToListAsync();
    }

    public async Task<BmadMemory> UpsertMemoryAsync(string key, string value, int? projectId, string? agentName, string category = "General")
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var existing = await db.BmadMemories
            .FirstOrDefaultAsync(m => m.Key == key && m.ProjectId == projectId);

        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedByAgent = agentName;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }

        var memory = new BmadMemory
        {
            Key = key,
            Value = value,
            ProjectId = projectId,
            UpdatedByAgent = agentName,
            Category = category,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.BmadMemories.Add(memory);
        await db.SaveChangesAsync();
        return memory;
    }

    public async Task DeleteMemoryAsync(int id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var memory = await db.BmadMemories.FindAsync(id);
        if (memory != null)
        {
            db.BmadMemories.Remove(memory);
            await db.SaveChangesAsync();
        }
    }
}
