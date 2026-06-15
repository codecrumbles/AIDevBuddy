using AIDevBuddy.Data;
using Microsoft.EntityFrameworkCore;

namespace AIDevBuddy.Services;

public class MessageService : IMessageService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public MessageService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<AgentMessage>> GetAllMessagesAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.AgentMessages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Include(m => m.Project)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();
    }

    public async Task<List<AgentMessage>> GetMessagesForAgentAsync(int agentId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.AgentMessages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Include(m => m.Project)
            .Where(m => m.ReceiverId == agentId || m.SenderId == agentId || m.IsBroadcast)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();
    }

    public async Task<List<AgentMessage>> GetMessagesForProjectAsync(int projectId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.AgentMessages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task<AgentMessage> SendMessageAsync(AgentMessage message)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        message.SentAt = DateTime.UtcNow;
        db.AgentMessages.Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    public async Task MarkAsReadAsync(int messageId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var message = await db.AgentMessages.FindAsync(messageId);
        if (message != null)
        {
            message.IsRead = true;
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> GetUnreadCountAsync(int agentId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.AgentMessages
            .CountAsync(m => (m.ReceiverId == agentId || m.IsBroadcast) && !m.IsRead);
    }
}
