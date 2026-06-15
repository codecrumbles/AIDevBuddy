using AIDevBuddy.Data;
using Microsoft.EntityFrameworkCore;

namespace AIDevBuddy.Services;

public class KanbanService : IKanbanService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public KanbanService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<KanbanBoard>> GetAllBoardsAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.KanbanBoards.OrderBy(b => b.Title).ToListAsync();
    }

    public async Task<KanbanBoard?> GetBoardWithColumnsAsync(int boardId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.KanbanBoards
            .Include(b => b.Columns.OrderBy(c => c.Order))
                .ThenInclude(c => c.Cards.OrderBy(k => k.Order))
                    .ThenInclude(k => k.AssignedAgent)
            .Include(b => b.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(k => k.Project)
            .FirstOrDefaultAsync(b => b.Id == boardId);
    }

    public async Task<KanbanBoard> CreateBoardAsync(KanbanBoard board)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        board.CreatedAt = DateTime.UtcNow;
        db.KanbanBoards.Add(board);
        await db.SaveChangesAsync();
        return board;
    }

    public async Task<KanbanCard> CreateCardAsync(KanbanCard card)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        card.CreatedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;
        db.KanbanCards.Add(card);
        await db.SaveChangesAsync();
        return card;
    }

    public async Task<KanbanCard> UpdateCardAsync(KanbanCard card)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        card.UpdatedAt = DateTime.UtcNow;
        db.KanbanCards.Update(card);
        await db.SaveChangesAsync();
        return card;
    }

    public async Task DeleteCardAsync(int cardId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var card = await db.KanbanCards.FindAsync(cardId);
        if (card != null)
        {
            db.KanbanCards.Remove(card);
            await db.SaveChangesAsync();
        }
    }

    public async Task<KanbanCard> MoveCardAsync(int cardId, int targetColumnId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var card = await db.KanbanCards.FindAsync(cardId)
            ?? throw new InvalidOperationException($"Card {cardId} not found.");
        card.ColumnId = targetColumnId;
        card.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return card;
    }
}
