using AIDevBuddy.Data;
using AIDevBuddy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AIDevBuddy.Tests;

[TestClass]
public class KanbanServiceTests
{
    private DbContextOptions<AppDbContext> _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private IDbContextFactory<AppDbContext> CreateFactory()
    {
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(default))
               .ReturnsAsync(() => new AppDbContext(_options));
        return factory.Object;
    }

    private async Task SeedAsync()
    {
        await using var db = new AppDbContext(_options);
        var board = new KanbanBoard { Id = 1, Title = "Test Board", CreatedAt = DateTime.UtcNow };
        db.KanbanBoards.Add(board);
        db.KanbanColumns.AddRange(
            new KanbanColumn { Id = 1, Title = "To Do", Status = KanbanStatus.ToDo, Order = 0, BoardId = 1 },
            new KanbanColumn { Id = 2, Title = "Done", Status = KanbanStatus.Done, Order = 1, BoardId = 1 }
        );
        db.KanbanCards.Add(new KanbanCard { Id = 1, Title = "Card 1", ColumnId = 1, Order = 0, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetAllBoardsAsync_ReturnsBoards()
    {
        await SeedAsync();
        var service = new KanbanService(CreateFactory());
        var boards = await service.GetAllBoardsAsync();
        Assert.AreEqual(1, boards.Count);
        Assert.AreEqual("Test Board", boards[0].Title);
    }

    [TestMethod]
    public async Task GetBoardWithColumnsAsync_ReturnsFullBoard()
    {
        await SeedAsync();
        var service = new KanbanService(CreateFactory());
        var board = await service.GetBoardWithColumnsAsync(1);
        Assert.IsNotNull(board);
        Assert.AreEqual(2, board.Columns.Count);
        Assert.AreEqual(1, board.Columns.First().Cards.Count);
    }

    [TestMethod]
    public async Task CreateCardAsync_AddsCard()
    {
        await SeedAsync();
        var service = new KanbanService(CreateFactory());
        var card = new KanbanCard { Title = "New Card", ColumnId = 1 };
        var result = await service.CreateCardAsync(card);
        Assert.IsTrue(result.Id > 0);
    }

    [TestMethod]
    public async Task MoveCardAsync_MovesCardToTargetColumn()
    {
        await SeedAsync();
        var service = new KanbanService(CreateFactory());
        await service.MoveCardAsync(1, 2);
        var board = await service.GetBoardWithColumnsAsync(1);
        var doneCol = board!.Columns.First(c => c.Id == 2);
        Assert.AreEqual(1, doneCol.Cards.Count);
    }

    [TestMethod]
    public async Task DeleteCardAsync_RemovesCard()
    {
        await SeedAsync();
        var service = new KanbanService(CreateFactory());
        await service.DeleteCardAsync(1);
        var board = await service.GetBoardWithColumnsAsync(1);
        Assert.AreEqual(0, board!.Columns.First(c => c.Id == 1).Cards.Count);
    }

    [TestMethod]
    public async Task UpdateCardAsync_UpdatesTitle()
    {
        await SeedAsync();
        var service = new KanbanService(CreateFactory());
        var card = new KanbanCard { Id = 1, Title = "Updated Card", ColumnId = 1, CreatedAt = DateTime.UtcNow };
        await service.UpdateCardAsync(card);
        var board = await service.GetBoardWithColumnsAsync(1);
        Assert.AreEqual("Updated Card", board!.Columns.First(c => c.Id == 1).Cards.First().Title);
    }
}
