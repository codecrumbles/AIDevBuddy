using AIDevBuddy.Data;
using AIDevBuddy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AIDevBuddy.Tests;

[TestClass]
public class MessageServiceTests
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
        db.Agents.AddRange(
            new Agent { Id = 1, Name = "Sender", Role = AgentRole.Analyst, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Agent { Id = 2, Name = "Receiver", Role = AgentRole.Developer, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        db.AgentMessages.AddRange(
            new AgentMessage { Id = 1, Content = "Hello", SenderId = 1, ReceiverId = 2, IsRead = false, SentAt = DateTime.UtcNow },
            new AgentMessage { Id = 2, Content = "Broadcast", IsBroadcast = true, IsRead = false, SentAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetAllMessagesAsync_ReturnsAllMessages()
    {
        await SeedAsync();
        var service = new MessageService(CreateFactory());
        var messages = await service.GetAllMessagesAsync();
        Assert.AreEqual(2, messages.Count);
    }

    [TestMethod]
    public async Task GetMessagesForAgentAsync_ReturnsRelevantMessages()
    {
        await SeedAsync();
        var service = new MessageService(CreateFactory());
        var messages = await service.GetMessagesForAgentAsync(2);
        Assert.AreEqual(2, messages.Count);
    }

    [TestMethod]
    public async Task SendMessageAsync_AddsMessage()
    {
        var service = new MessageService(CreateFactory());
        var msg = new AgentMessage { Content = "Test message", IsBroadcast = true };
        var result = await service.SendMessageAsync(msg);
        Assert.IsTrue(result.Id > 0);
    }

    [TestMethod]
    public async Task MarkAsReadAsync_MarksMessage()
    {
        await SeedAsync();
        var service = new MessageService(CreateFactory());
        await service.MarkAsReadAsync(1);
        var messages = await service.GetAllMessagesAsync();
        var msg = messages.First(m => m.Id == 1);
        Assert.IsTrue(msg.IsRead);
    }

    [TestMethod]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        await SeedAsync();
        var service = new MessageService(CreateFactory());
        var count = await service.GetUnreadCountAsync(2);
        Assert.AreEqual(2, count);
    }
}
