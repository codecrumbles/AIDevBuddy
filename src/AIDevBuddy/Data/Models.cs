using System.ComponentModel.DataAnnotations;

namespace AIDevBuddy.Data;

public enum AgentRole
{
    Analyst,
    ProductManager,
    Architect,
    ProductOwner,
    ScrumMaster,
    Developer,
    QA,
    Orchestrator
}

public enum AgentStatus
{
    Idle,
    Working,
    WaitingForInput,
    Done,
    Error
}

public enum KanbanStatus
{
    Backlog,
    ToDo,
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
    public AgentRole Role { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Idle;
    [MaxLength(500)]
    public string? Description { get; set; }
    public string? SystemPrompt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<AgentMessage> SentMessages { get; set; } = new List<AgentMessage>();
    public ICollection<AgentMessage> ReceivedMessages { get; set; } = new List<AgentMessage>();
    public ICollection<KanbanCard> AssignedCards { get; set; } = new List<KanbanCard>();
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
    [MaxLength(50)]
    public string? StoryType { get; set; } = "Story";
    public string? Tags { get; set; }
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
    [MaxLength(100)]
    public string? MessageType { get; set; } = "Chat";
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
