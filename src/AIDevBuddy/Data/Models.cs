using System.ComponentModel.DataAnnotations;

namespace AIDevBuddy.Data;

// BMAD roles matching the official personas:
// Mary=Analyst, Paige=TechWriter, John=ProductManager, Sally=UxDesigner,
// Winston=Architect, Bob=ScrumMaster, Amelia=Developer, Quinn=QA, Barry=QuickFlowDev
public enum AgentRole
{
    Analyst,        // Mary  — Analysis phase
    TechWriter,     // Paige — Analysis phase
    ProductManager, // John  — Planning phase
    UxDesigner,     // Sally — Planning phase
    Architect,      // Winston — Solutioning phase
    ScrumMaster,    // Bob   — Implementation phase
    Developer,      // Amelia — Implementation phase
    QA,             // Quinn — Implementation phase
    QuickFlowDev    // Barry — All phases (solo track)
}

public enum AgentStatus
{
    Idle,
    Working,
    WaitingForInput,
    Done,
    Error
}

// Maps to BMAD story status state machine:
// Backlog → ReadyForDev → InProgress → Review → Done
public enum KanbanStatus
{
    Backlog,
    ReadyForDev,
    InProgress,
    Review,
    Done
}

public enum LlmProvider
{
    Copilot,
    GLM,
    Claude,
    LocalLlm
}

public class Agent
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    // Human persona name (Mary, John, Sally, etc.)
    [MaxLength(50)]
    public string? PersonaName { get; set; }
    public AgentRole Role { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Idle;
    [MaxLength(500)]
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    // BMAD phase: Analysis, Planning, Solutioning, Implementation, All
    [MaxLength(50)]
    public string? BmadPhase { get; set; }
    // Comma-separated trigger codes e.g. "BP,MR,DR,CB"
    [MaxLength(200)]
    public string? TriggerCodes { get; set; }
    // Preferred Ollama model for this agent, e.g. "llama3.2:latest"
    [MaxLength(100)]
    public string? PreferredModel { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<AgentMessage> SentMessages { get; set; } = new List<AgentMessage>();
    public ICollection<AgentMessage> ReceivedMessages { get; set; } = new List<AgentMessage>();
    public ICollection<KanbanCard> AssignedCards { get; set; } = new List<KanbanCard>();
    public ICollection<ProjectAgentAssignment> ProjectAssignments { get; set; } = new List<ProjectAgentAssignment>();
}

public class KanbanBoard
{
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(1000)]
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<KanbanColumn> Columns { get; set; } = new List<KanbanColumn>();
}

public class KanbanColumn
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;
    public KanbanStatus Status { get; set; }
    public int Order { get; set; }
    public int BoardId { get; set; }
    public KanbanBoard Board { get; set; } = null!;
    public ICollection<KanbanCard> Cards { get; set; } = new List<KanbanCard>();
}

public class KanbanCard
{
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Description { get; set; }
    public int Order { get; set; }
    public int ColumnId { get; set; }
    public KanbanColumn Column { get; set; } = null!;
    public int? AssignedAgentId { get; set; }
    public Agent? AssignedAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(50)]
    public string? Priority { get; set; } = "Medium";
    // BMAD story types: Story, Epic, Task, Bug, Spike
    [MaxLength(50)]
    public string? StoryType { get; set; } = "Story";
    public string? Tags { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    // BMAD acceptance criteria (markdown)
    public string? AcceptanceCriteria { get; set; }
}

public class AgentMessage
{
    public int Id { get; set; }
    [Required]
    public string Content { get; set; } = string.Empty;
    public int? SenderId { get; set; }
    public Agent? Sender { get; set; }
    public int? ReceiverId { get; set; }
    public Agent? Receiver { get; set; }
    public bool IsRead { get; set; } = false;
    public bool IsBroadcast { get; set; } = false;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    // Chat, Handoff, Request, Response, Broadcast, Alert, MemoryUpdate,
    // ProjectStart, TriggerCode, PhaseTransition, ReviewResult
    [MaxLength(100)]
    public string? MessageType { get; set; } = "Chat";
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    [MaxLength(200)]
    public string? MemoryKey { get; set; }
}

public class LlmSetting
{
    public int Id { get; set; }
    public LlmProvider Provider { get; set; }
    [MaxLength(200)]
    public string? ApiKey { get; set; }
    [MaxLength(200)]
    public string? Endpoint { get; set; }
    [MaxLength(100)]
    public string? ModelName { get; set; }
    public bool IsEnabled { get; set; } = false;
    public bool IsDefault { get; set; } = false;
    [MaxLength(500)]
    public string? LocalModelPath { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Project
{
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? Description { get; set; }
    // Planning, Active, Review, Complete, On Hold
    [MaxLength(50)]
    public string Status { get; set; } = "Planning";
    // Current BMAD phase: Analysis, Planning, Solutioning, Implementation
    [MaxLength(50)]
    public string BmadPhase { get; set; } = "Analysis";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? LeadAgentId { get; set; }
    public Agent? LeadAgent { get; set; }
    // AutoLoop — autonomous agent-driven execution
    public bool AutoLoop { get; set; } = false;
    // Idle, Running, Paused, Complete, Error
    [MaxLength(50)]
    public string AutoLoopStatus { get; set; } = "Idle";
    public int AutoLoopIntervalSeconds { get; set; } = 30;
    public ICollection<ProjectAgentAssignment> AgentAssignments { get; set; } = new List<ProjectAgentAssignment>();
    public ICollection<AgentMessage> Messages { get; set; } = new List<AgentMessage>();
    public ICollection<BmadMemory> Memories { get; set; } = new List<BmadMemory>();
}

public class ProjectAgentAssignment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    [MaxLength(100)]
    public string? Role { get; set; }
}

// BMAD shared context — equivalent to project-context.md / config.yaml
public class BmadMemory
{
    public int Id { get; set; }
    // Dot-notation key e.g. "project.name", "bmad.phase", "config.skill-level"
    [Required, MaxLength(200)]
    public string Key { get; set; } = string.Empty;
    [Required]
    public string Value { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    [MaxLength(100)]
    public string? UpdatedByAgent { get; set; }
    // Project, BMAD, Config, Artifact, Custom
    [MaxLength(50)]
    public string Category { get; set; } = "General";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
