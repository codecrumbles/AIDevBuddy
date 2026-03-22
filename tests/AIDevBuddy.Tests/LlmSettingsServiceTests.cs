using AIDevBuddy.Data;
using AIDevBuddy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AIDevBuddy.Tests;

[TestClass]
public class LlmSettingsServiceTests
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
        db.LlmSettings.AddRange(
            new LlmSetting { Id = 1, Provider = LlmProvider.Copilot, IsEnabled = false, IsDefault = false, UpdatedAt = DateTime.UtcNow },
            new LlmSetting { Id = 2, Provider = LlmProvider.Claude, IsEnabled = true, IsDefault = true, UpdatedAt = DateTime.UtcNow },
            new LlmSetting { Id = 3, Provider = LlmProvider.GLM, IsEnabled = false, IsDefault = false, UpdatedAt = DateTime.UtcNow },
            new LlmSetting { Id = 4, Provider = LlmProvider.LocalLlm, IsEnabled = false, IsDefault = false, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task GetAllSettingsAsync_ReturnsAllSettings()
    {
        await SeedAsync();
        var service = new LlmSettingsService(CreateFactory());
        var settings = await service.GetAllSettingsAsync();
        Assert.AreEqual(4, settings.Count);
    }

    [TestMethod]
    public async Task GetSettingByProviderAsync_ReturnsCorrectSetting()
    {
        await SeedAsync();
        var service = new LlmSettingsService(CreateFactory());
        var setting = await service.GetSettingByProviderAsync(LlmProvider.Claude);
        Assert.IsNotNull(setting);
        Assert.IsTrue(setting.IsDefault);
    }

    [TestMethod]
    public async Task UpdateSettingAsync_UpdatesApiKey()
    {
        await SeedAsync();
        var service = new LlmSettingsService(CreateFactory());
        var setting = await service.GetSettingByProviderAsync(LlmProvider.Copilot);
        setting!.ApiKey = "test-api-key";
        await service.UpdateSettingAsync(setting);

        var updated = await service.GetSettingByProviderAsync(LlmProvider.Copilot);
        Assert.AreEqual("test-api-key", updated!.ApiKey);
    }

    [TestMethod]
    public async Task SetDefaultProviderAsync_SetsOnlyOneDefault()
    {
        await SeedAsync();
        var service = new LlmSettingsService(CreateFactory());
        await service.SetDefaultProviderAsync(LlmProvider.Copilot);

        var settings = await service.GetAllSettingsAsync();
        var defaults = settings.Where(s => s.IsDefault).ToList();
        Assert.AreEqual(1, defaults.Count);
        Assert.AreEqual(LlmProvider.Copilot, defaults[0].Provider);
    }
}
