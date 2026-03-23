using AIDevBuddy.Data;

namespace AIDevBuddy.Services;

public interface ILlmSettingsService
{
    Task<List<LlmSetting>> GetAllSettingsAsync();
    Task<LlmSetting?> GetSettingByProviderAsync(LlmProvider provider);
    Task<LlmSetting> UpdateSettingAsync(LlmSetting setting);
    Task<LlmSetting> SetDefaultProviderAsync(LlmProvider provider);
}
