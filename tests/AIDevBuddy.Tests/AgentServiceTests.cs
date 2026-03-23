using AIDevBuddy.Data;
using AIDevBuddy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AIDevBuddy.Tests;

[TestClass]
public class AgentServiceTests
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

    private async Task SeedAgentsAsync()
    {
        await using var db = new AppDbContext(_options);
        db.Agents.AddRange(
            new Agent { Id = 1, Name = "Analyst", Role = AgentRole.Analyst, Status = AgentStatus.Idle, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Agent { Id = 2, Name = "Developer", Role = AgentRole.Developer, Status = AgentStatus.Working, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetAllAgentsAsync_ReturnsAllAgents()
    {
        await SeedAgentsAsync();
        var service = new AgentService(CreateFactory());
        var result = await service.GetAllAgentsAsync();
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetAgentByIdAsync_ExistingId_ReturnsAgent()
    {
        await SeedAgentsAsync();
        var service = new AgentService(CreateFactory());
        var agent = await service.GetAgentByIdAsync(1);
        Assert.IsNotNull(agent);
        Assert.AreEqual("Analyst", agent.Name);
    }

    [TestMethod]
    public async Task GetAgentByIdAsync_NonExistingId_ReturnsNull()
    {
        await SeedAgentsAsync();
        var service = new AgentService(CreateFactory());
        var agent = await service.GetAgentByIdAsync(999);
        Assert.IsNull(agent);
    }

    [TestMethod]
    public async Task CreateAgentAsync_AddsAgent()
    {
        var service = new AgentService(CreateFactory());
        var newAgent = new Agent { Name = "QA Agent", Role = AgentRole.QA };
        var result = await service.CreateAgentAsync(newAgent);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Id > 0);

        var agents = await service.GetAllAgentsAsync();
        Assert.AreEqual(1, agents.Count);
        Assert.AreEqual("QA Agent", agents[0].Name);
    }

    [TestMethod]
    public async Task UpdateAgentAsync_UpdatesAgent()
    {
        await SeedAgentsAsync();
        var service = new AgentService(CreateFactory());
        var agent = await service.GetAgentByIdAsync(1);
        agent!.Name = "Updated Analyst";
        await service.UpdateAgentAsync(agent);

        var updated = await service.GetAgentByIdAsync(1);
        Assert.AreEqual("Updated Analyst", updated!.Name);
    }

    [TestMethod]
    public async Task DeleteAgentAsync_RemovesAgent()
    {
        await SeedAgentsAsync();
        var service = new AgentService(CreateFactory());
        await service.DeleteAgentAsync(1);
        var agents = await service.GetAllAgentsAsync();
        Assert.AreEqual(1, agents.Count);
        Assert.AreEqual(2, agents[0].Id);
    }

    [TestMethod]
    public async Task UpdateAgentStatusAsync_UpdatesStatus()
    {
        await SeedAgentsAsync();
        var service = new AgentService(CreateFactory());
        await service.UpdateAgentStatusAsync(1, AgentStatus.Working);
        var agent = await service.GetAgentByIdAsync(1);
        Assert.AreEqual(AgentStatus.Working, agent!.Status);
    }

    [TestMethod]
    public async Task UpdateAgentStatusAsync_InvalidId_ThrowsException()
    {
        var service = new AgentService(CreateFactory());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.UpdateAgentStatusAsync(999, AgentStatus.Done));
    }
}
