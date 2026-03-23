using Microsoft.EntityFrameworkCore;

namespace AIDevBuddy.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<KanbanBoard> KanbanBoards => Set<KanbanBoard>();
    public DbSet<KanbanColumn> KanbanColumns => Set<KanbanColumn>();
    public DbSet<KanbanCard> KanbanCards => Set<KanbanCard>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
    public DbSet<LlmSetting> LlmSettings => Set<LlmSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(m => m.Sender)
            .WithMany(a => a.SentMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AgentMessage>()
            .HasOne(m => m.Receiver)
            .WithMany(a => a.ReceivedMessages)
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KanbanCard>()
            .HasOne(c => c.AssignedAgent)
            .WithMany(a => a.AssignedCards)
            .HasForeignKey(c => c.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KanbanColumn>()
            .HasOne(col => col.Board)
            .WithMany(b => b.Columns)
            .HasForeignKey(col => col.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<KanbanCard>()
            .HasOne(c => c.Column)
            .WithMany(col => col.Cards)
            .HasForeignKey(c => c.ColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed default board with BMAD columns
        modelBuilder.Entity<KanbanBoard>().HasData(
            new KanbanBoard { Id = 1, Title = "BMAD Sprint Board", Description = "AI-driven development sprint board following the BMAD methodology", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
        modelBuilder.Entity<KanbanColumn>().HasData(
            new KanbanColumn { Id = 1, Title = "Backlog", Status = KanbanStatus.Backlog, Order = 0, BoardId = 1 },
            new KanbanColumn { Id = 2, Title = "To Do", Status = KanbanStatus.ToDo, Order = 1, BoardId = 1 },
            new KanbanColumn { Id = 3, Title = "In Progress", Status = KanbanStatus.InProgress, Order = 2, BoardId = 1 },
            new KanbanColumn { Id = 4, Title = "Review", Status = KanbanStatus.Review, Order = 3, BoardId = 1 },
            new KanbanColumn { Id = 5, Title = "Done", Status = KanbanStatus.Done, Order = 4, BoardId = 1 }
        );
        // Seed default agents
        modelBuilder.Entity<Agent>().HasData(
            new Agent { Id = 1, Name = "Analyst", Role = AgentRole.Analyst, Status = AgentStatus.Idle, Description = "Gathers requirements and produces initial briefs", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Agent { Id = 2, Name = "Product Manager", Role = AgentRole.ProductManager, Status = AgentStatus.Idle, Description = "Produces PRDs and feature specifications", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Agent { Id = 3, Name = "Architect", Role = AgentRole.Architect, Status = AgentStatus.Idle, Description = "Maps technical blueprints and architecture", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Agent { Id = 4, Name = "Product Owner", Role = AgentRole.ProductOwner, Status = AgentStatus.Idle, Description = "Refines specs and manages backlog", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Agent { Id = 5, Name = "Scrum Master", Role = AgentRole.ScrumMaster, Status = AgentStatus.Idle, Description = "Translates specs to development stories", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Agent { Id = 6, Name = "Developer", Role = AgentRole.Developer, Status = AgentStatus.Idle, Description = "Implements stories and writes code", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Agent { Id = 7, Name = "QA Engineer", Role = AgentRole.QA, Status = AgentStatus.Idle, Description = "Tests and validates implementations", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Agent { Id = 8, Name = "Orchestrator", Role = AgentRole.Orchestrator, Status = AgentStatus.Idle, Description = "Coordinates multi-agent interactions", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
        // Seed default LLM settings
        modelBuilder.Entity<LlmSetting>().HasData(
            new LlmSetting { Id = 1, Provider = LlmProvider.Copilot, IsEnabled = false, IsDefault = false, ModelName = "gpt-4o", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LlmSetting { Id = 2, Provider = LlmProvider.GLM, IsEnabled = false, IsDefault = false, ModelName = "glm-4", Endpoint = "https://open.bigmodel.cn/api/paas/v4/", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LlmSetting { Id = 3, Provider = LlmProvider.Claude, IsEnabled = false, IsDefault = false, ModelName = "claude-3-5-sonnet-20241022", Endpoint = "https://api.anthropic.com/v1/", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LlmSetting { Id = 4, Provider = LlmProvider.LocalLlm, IsEnabled = false, IsDefault = false, ModelName = "llama3", Endpoint = "http://localhost:11434/api/", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
