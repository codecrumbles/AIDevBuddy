using AIDevBuddy.Components;
using AIDevBuddy.Data;
using AIDevBuddy.Services;
using Microsoft.EntityFrameworkCore;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Radzen services
builder.Services.AddRadzenComponents();

// Database
var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIDevBuddy", "aidevbuddy.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Application services
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IKanbanService, KanbanService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<ILlmSettingsService, LlmSettingsService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IMemoryService, MemoryService>();

// LLM router — scoped; routes to whichever provider is active
builder.Services.AddSingleton<ICopilotService, CopilotService>();
builder.Services.AddScoped<ILlmChatService, LlmChatService>();

// Workspace: singleton — manages on-disk project folders
builder.Services.AddSingleton<IWorkspaceService, WorkspaceService>();

// AutoLoop: singleton so background loops survive Blazor component disposal
builder.Services.AddSingleton<AutoLoopService>();
builder.Services.AddSingleton<IAutoLoopService>(sp => sp.GetRequiredService<AutoLoopService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<AutoLoopService>());

var app = builder.Build();

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
