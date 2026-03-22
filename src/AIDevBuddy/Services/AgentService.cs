using AIDevBuddy.Data;
using Microsoft.EntityFrameworkCore;

namespace AIDevBuddy.Services;

public class AgentService : IAgentService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public AgentService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<Agent>> GetAllAgentsAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.Agents.OrderBy(a => a.Role).ToListAsync();
    }

    public async Task<Agent?> GetAgentByIdAsync(int id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.Agents.FindAsync(id);
    }

    public async Task<Agent> CreateAgentAsync(Agent agent)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        agent.CreatedAt = DateTime.UtcNow;
        agent.UpdatedAt = DateTime.UtcNow;
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    public async Task<Agent> UpdateAgentAsync(Agent agent)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        agent.UpdatedAt = DateTime.UtcNow;
        db.Agents.Update(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    public async Task DeleteAgentAsync(int id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var agent = await db.Agents.FindAsync(id);
        if (agent != null)
        {
            db.Agents.Remove(agent);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Agent> UpdateAgentStatusAsync(int id, AgentStatus status)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var agent = await db.Agents.FindAsync(id)
            ?? throw new InvalidOperationException($"Agent with ID {id} not found.");
        agent.Status = status;
        agent.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return agent;
    }
}
