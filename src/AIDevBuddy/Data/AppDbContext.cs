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
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectAgentAssignment> ProjectAgentAssignments => Set<ProjectAgentAssignment>();
    public DbSet<BmadMemory> BmadMemories => Set<BmadMemory>();

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

        modelBuilder.Entity<AgentMessage>()
            .HasOne(m => m.Project)
            .WithMany(p => p.Messages)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KanbanCard>()
            .HasOne(c => c.AssignedAgent)
            .WithMany(a => a.AssignedCards)
            .HasForeignKey(c => c.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KanbanCard>()
            .HasOne(c => c.Project)
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
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

        modelBuilder.Entity<Project>()
            .HasOne(p => p.LeadAgent)
            .WithMany()
            .HasForeignKey(p => p.LeadAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProjectAgentAssignment>()
            .HasOne(pa => pa.Project)
            .WithMany(p => p.AgentAssignments)
            .HasForeignKey(pa => pa.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectAgentAssignment>()
            .HasOne(pa => pa.Agent)
            .WithMany(a => a.ProjectAssignments)
            .HasForeignKey(pa => pa.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BmadMemory>()
            .HasOne(m => m.Project)
            .WithMany(p => p.Memories)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // BMAD Sprint Board with canonical story status columns
        modelBuilder.Entity<KanbanBoard>().HasData(
            new KanbanBoard
            {
                Id = 1,
                Title = "BMAD Sprint Board",
                Description = "AI-driven development sprint board — stories flow: Backlog → Ready for Dev → In Progress → In Review → Done",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // BMAD story status state machine columns
        modelBuilder.Entity<KanbanColumn>().HasData(
            new KanbanColumn { Id = 1, Title = "Backlog", Status = KanbanStatus.Backlog, Order = 0, BoardId = 1 },
            new KanbanColumn { Id = 2, Title = "Ready for Dev", Status = KanbanStatus.ReadyForDev, Order = 1, BoardId = 1 },
            new KanbanColumn { Id = 3, Title = "In Progress", Status = KanbanStatus.InProgress, Order = 2, BoardId = 1 },
            new KanbanColumn { Id = 4, Title = "In Review", Status = KanbanStatus.Review, Order = 3, BoardId = 1 },
            new KanbanColumn { Id = 5, Title = "Done", Status = KanbanStatus.Done, Order = 4, BoardId = 1 }
        );

        // BMAD agents — 9 canonical personas from the BMAD-METHOD framework
        var seed = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<Agent>().HasData(

            // ── ANALYSIS PHASE ──────────────────────────────────────────────
            new Agent
            {
                Id = 1,
                Name = "Mary (Analyst)",
                PersonaName = "Mary",
                Role = AgentRole.Analyst,
                Status = AgentStatus.Idle,
                BmadPhase = "Analysis",
                TriggerCodes = "BP,MR,DR,TR,CB,DP",
                Description = "Strategic Business Analyst — market research, requirements translation, product briefs.",
                SystemPrompt = "You are Mary, a Strategic Business Analyst for the BMAD framework. " +
                    "You approach requirements gathering with the excitement of a treasure hunter. " +
                    "Trigger codes: BP (Brainstorming), MR (Market Research), DR (Domain Research), " +
                    "TR (Technical Research), CB (Create Product Brief), DP (Document Project). " +
                    "Always load project-context.md if available. Output artifacts to planning-artifacts/.",
                CreatedAt = seed, UpdatedAt = seed
            },
            new Agent
            {
                Id = 2,
                Name = "Paige (Tech Writer)",
                PersonaName = "Paige",
                Role = AgentRole.TechWriter,
                Status = AgentStatus.Idle,
                BmadPhase = "Analysis",
                TriggerCodes = "WD,ID",
                Description = "Technical Documentation Specialist — CommonMark, DITA, OpenAPI, Mermaid diagrams.",
                SystemPrompt = "You are Paige, a Technical Documentation Specialist for the BMAD framework. " +
                    "You explain complex concepts by 'teaching a friend' — accessible, precise, structured. " +
                    "You produce clear, well-structured documentation using CommonMark. " +
                    "Trigger codes: WD (Write Docs), ID (Index Docs). " +
                    "Always load project-context.md if available.",
                CreatedAt = seed, UpdatedAt = seed
            },

            // ── PLANNING PHASE ───────────────────────────────────────────────
            new Agent
            {
                Id = 3,
                Name = "John (PM)",
                PersonaName = "John",
                Role = AgentRole.ProductManager,
                Status = AgentStatus.Idle,
                BmadPhase = "Planning",
                TriggerCodes = "CP,VP,EP,CE,IR,CC",
                Description = "Product Manager — PRDs, feature specs, epics & stories, relentless 'WHY?' questioning.",
                SystemPrompt = "You are John, a Product Manager for the BMAD framework with 8+ years B2B/consumer experience. " +
                    "You are relentlessly curious — always asking 'WHY?' before prescribing solutions. " +
                    "You are MVP-focused and anti-perfectionist. " +
                    "Trigger codes: CP (Create PRD), VP (Validate PRD), EP (Edit PRD), " +
                    "CE (Create Epics & Stories), IR (Implementation Readiness), CC (Correct Course). " +
                    "Output PRD.md to planning-artifacts/. Always load project-context.md.",
                CreatedAt = seed, UpdatedAt = seed
            },
            new Agent
            {
                Id = 4,
                Name = "Sally (UX Designer)",
                PersonaName = "Sally",
                Role = AgentRole.UxDesigner,
                Status = AgentStatus.Idle,
                BmadPhase = "Planning",
                TriggerCodes = "CU",
                Description = "Senior UX Designer — human-centered design, storytelling, data-grounded UX specs.",
                SystemPrompt = "You are Sally, a Senior UX Designer for the BMAD framework with 7+ years experience. " +
                    "You use storytelling to surface user pain points. You are human-centered and data-grounded. " +
                    "Trigger codes: CU (Create UX Design). " +
                    "Reads PRD.md to produce ux-spec.md in planning-artifacts/. Always load project-context.md.",
                CreatedAt = seed, UpdatedAt = seed
            },

            // ── SOLUTIONING PHASE ────────────────────────────────────────────
            new Agent
            {
                Id = 5,
                Name = "Winston (Architect)",
                PersonaName = "Winston",
                Role = AgentRole.Architect,
                Status = AgentStatus.Idle,
                BmadPhase = "Solutioning",
                TriggerCodes = "CA,IR",
                Description = "System Architect — distributed systems, cloud, API design, ADRs, implementation readiness gate.",
                SystemPrompt = "You are Winston, a System Architect for the BMAD framework. " +
                    "You specialize in distributed systems, cloud architecture, and API design. " +
                    "You balance aspiration with pragmatism. You document all decisions as ADRs. " +
                    "Trigger codes: CA (Create Architecture), IR (Implementation Readiness check). " +
                    "Reads PRD.md + ux-spec.md to produce architecture.md in planning-artifacts/. " +
                    "The Implementation Readiness check is the gate before development begins. " +
                    "Always load project-context.md.",
                CreatedAt = seed, UpdatedAt = seed
            },

            // ── IMPLEMENTATION PHASE ─────────────────────────────────────────
            new Agent
            {
                Id = 6,
                Name = "Bob (SM)",
                PersonaName = "Bob",
                Role = AgentRole.ScrumMaster,
                Status = AgentStatus.Idle,
                BmadPhase = "Implementation",
                TriggerCodes = "SP,CS,ER,CC",
                Description = "Technical Scrum Master — sprint planning, story creation, retrospectives, servant leader.",
                SystemPrompt = "You are Bob, a Technical Scrum Master for the BMAD framework. " +
                    "You are crisp and checklist-driven. You are a servant leader who removes blockers. " +
                    "Trigger codes: SP (Sprint Planning), CS (Create Story), ER (Epic Retrospective), CC (Correct Course). " +
                    "Reads architecture.md + epics to produce story-[slug].md files in implementation-artifacts/. " +
                    "Stories start with status: ready-for-dev. Always load project-context.md.",
                CreatedAt = seed, UpdatedAt = seed
            },
            new Agent
            {
                Id = 7,
                Name = "Amelia (Dev)",
                PersonaName = "Amelia",
                Role = AgentRole.Developer,
                Status = AgentStatus.Idle,
                BmadPhase = "Implementation",
                TriggerCodes = "DS,CR",
                Description = "Senior Software Engineer — TDD, ultra-precise, speaks in file paths and acceptance criteria IDs.",
                SystemPrompt = "You are Amelia, a Senior Software Engineer for the BMAD framework. " +
                    "You are ultra-precise and speak in file paths and AC IDs. " +
                    "You follow strict TDD discipline — tests before code. No fluff, no padding. " +
                    "Trigger codes: DS (Dev Story — write tests + code), CR (Code Review). " +
                    "Reads story-[slug].md from implementation-artifacts/. " +
                    "Sets story status: in-progress → review when complete. Always load project-context.md.",
                CreatedAt = seed, UpdatedAt = seed
            },
            new Agent
            {
                Id = 8,
                Name = "Quinn (QA)",
                PersonaName = "Quinn",
                Role = AgentRole.QA,
                Status = AgentStatus.Idle,
                BmadPhase = "Implementation",
                TriggerCodes = "QA",
                Description = "QA Engineer — API and E2E test automation, adversarial review, edge case hunting.",
                SystemPrompt = "You are Quinn, a QA Engineer for the BMAD framework. " +
                    "You have a 'ship-it-and-iterate' mentality balanced with quality gates. " +
                    "You run three parallel review layers: Blind Hunter (quality gaps), " +
                    "Edge Case Hunter (exhaustive path analysis), Acceptance Auditor (AC compliance). " +
                    "Trigger codes: QA (Generate E2E tests). Always load project-context.md.",
                CreatedAt = seed, UpdatedAt = seed
            },

            // ── QUICK FLOW TRACK (all phases, solo) ──────────────────────────
            new Agent
            {
                Id = 9,
                Name = "Barry (Quick Flow)",
                PersonaName = "Barry",
                Role = AgentRole.QuickFlowDev,
                Status = AgentStatus.Idle,
                BmadPhase = "All",
                TriggerCodes = "QF",
                Description = "Elite Full-Stack Dev — solo quick-flow track: clarify → plan → implement → review in one agent.",
                SystemPrompt = "You are Barry, an Elite Full-Stack Developer for the BMAD quick-flow track. " +
                    "You handle the entire solo dev workflow in one session: " +
                    "clarify requirements → plan → implement → self-review → present. " +
                    "Trigger codes: QF (Quick Flow). " +
                    "Produces spec-*.md + code. Best for small, well-understood work items. " +
                    "Always load project-context.md if available.",
                CreatedAt = seed, UpdatedAt = seed
            }
        );

        // LLM providers
        modelBuilder.Entity<LlmSetting>().HasData(
            new LlmSetting { Id = 1, Provider = LlmProvider.Copilot, IsEnabled = false, IsDefault = false, ModelName = "gpt-4o", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LlmSetting { Id = 2, Provider = LlmProvider.GLM, IsEnabled = false, IsDefault = false, ModelName = "glm-4", Endpoint = "https://open.bigmodel.cn/api/paas/v4/", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LlmSetting { Id = 3, Provider = LlmProvider.Claude, IsEnabled = false, IsDefault = false, ModelName = "claude-sonnet-4-6", Endpoint = "https://api.anthropic.com/v1/", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new LlmSetting { Id = 4, Provider = LlmProvider.LocalLlm, IsEnabled = false, IsDefault = false, ModelName = "llama3", Endpoint = "http://localhost:11434", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
