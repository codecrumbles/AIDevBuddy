using AIDevBuddy.Data;
using Microsoft.EntityFrameworkCore;

namespace AIDevBuddy.Services;

public class LlmSettingsService : ILlmSettingsService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public LlmSettingsService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<LlmSetting>> GetAllSettingsAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.LlmSettings.OrderBy(s => s.Provider).ToListAsync();
    }

    public async Task<LlmSetting?> GetSettingByProviderAsync(LlmProvider provider)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.LlmSettings.FirstOrDefaultAsync(s => s.Provider == provider);
    }

    public async Task<LlmSetting> UpdateSettingAsync(LlmSetting setting)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        setting.UpdatedAt = DateTime.UtcNow;
        db.LlmSettings.Update(setting);
        await db.SaveChangesAsync();
        return setting;
    }

    public async Task<LlmSetting> SetDefaultProviderAsync(LlmProvider provider)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var settings = await db.LlmSettings.ToListAsync();
        foreach (var s in settings)
        {
            s.IsDefault = s.Provider == provider;
            s.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return settings.First(s => s.Provider == provider);
    }
}
