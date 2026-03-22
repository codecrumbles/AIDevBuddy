using AIDevBuddy.Data;

namespace AIDevBuddy.Services;

public interface IKanbanService
{
    Task<List<KanbanBoard>> GetAllBoardsAsync();
    Task<KanbanBoard?> GetBoardWithColumnsAsync(int boardId);
    Task<KanbanBoard> CreateBoardAsync(KanbanBoard board);
    Task<KanbanCard> CreateCardAsync(KanbanCard card);
    Task<KanbanCard> UpdateCardAsync(KanbanCard card);
    Task DeleteCardAsync(int cardId);
    Task<KanbanCard> MoveCardAsync(int cardId, int targetColumnId);
}
