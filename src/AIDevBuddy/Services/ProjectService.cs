using AIDevBuddy.Data;
using Microsoft.EntityFrameworkCore;

namespace AIDevBuddy.Services;

public class ProjectService : IProjectService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IMessageService _messageService;
    private readonly ILlmChatService _llmChat;
    private readonly IWorkspaceService _workspace;

    public ProjectService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IMessageService messageService,
        ILlmChatService llmChat,
        IWorkspaceService workspace)
    {
        _dbContextFactory = dbContextFactory;
        _messageService = messageService;
        _llmChat = llmChat;
        _workspace = workspace;
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.Projects
            .Include(p => p.LeadAgent)
            .Include(p => p.AgentAssignments).ThenInclude(a => a.Agent)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.Projects
            .Include(p => p.LeadAgent)
            .Include(p => p.AgentAssignments).ThenInclude(a => a.Agent)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project> CreateProjectAsync(Project project, List<int>? agentIds = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        if (agentIds != null)
        {
            foreach (var agentId in agentIds)
            {
                db.ProjectAgentAssignments.Add(new ProjectAgentAssignment
                {
                    ProjectId = project.Id,
                    AgentId = agentId
                });
            }
            await db.SaveChangesAsync();
        }

        // Create on-disk workspace folder
        await _workspace.EnsureProjectFolderAsync(project.Name, project.Id);

        // Seed BMAD project-context memory
        var leadAgent = project.LeadAgentId.HasValue
            ? await db.Agents.FindAsync(project.LeadAgentId.Value)
            : null;
        var leadName = leadAgent?.PersonaName ?? leadAgent?.Name ?? "John";
        var now = DateTime.UtcNow;

        void AddMemory(string key, string value, string category) =>
            db.BmadMemories.Add(new BmadMemory
            {
                Key = key, Value = value, ProjectId = project.Id,
                Category = category, UpdatedByAgent = leadName,
                CreatedAt = now, UpdatedAt = now
            });

        AddMemory("project.name",        project.Name,                                   "Config");
        AddMemory("project.status",      project.Status,                                 "Config");
        AddMemory("project.lead-agent",  leadName,                                       "Config");
        if (!string.IsNullOrWhiteSpace(project.Description))
            AddMemory("project.description", project.Description,                        "Config");

        AddMemory("bmad.current-phase",  "Analysis",                                     "BMAD");
        AddMemory("bmad.workflow",       "Idle — awaiting trigger code",                 "BMAD");
        AddMemory("bmad.next-step",      "Mary: run BP (Brainstorming) or CB (Create Product Brief)", "BMAD");

        AddMemory("artifacts.planning",        "planning-artifacts/",                    "Artifacts");
        AddMemory("artifacts.implementation",  "implementation-artifacts/",              "Artifacts");
        AddMemory("artifacts.docs",            "docs/",                                  "Artifacts");
        AddMemory("artifacts.product-brief",   "pending",                                "Artifacts");
        AddMemory("artifacts.prd",             "pending",                                "Artifacts");
        AddMemory("artifacts.ux-spec",         "pending",                                "Artifacts");
        AddMemory("artifacts.architecture",    "pending",                                "Artifacts");
        AddMemory("artifacts.sprint-status",   "pending",                                "Artifacts");

        await db.SaveChangesAsync();

        await _messageService.SendMessageAsync(new AgentMessage
        {
            Content = $"🚀 Project '{project.Name}' created. BMAD Phase 1 — Analysis now active. " +
                      $"{(string.IsNullOrWhiteSpace(project.Description) ? "" : $"Goal: {project.Description} ")}" +
                      $"Mary: please run BP (Brainstorming) to kick off requirements gathering. " +
                      $"All agents: load project-context when assigned a workflow.",
            SenderId = project.LeadAgentId,
            ReceiverId = null,
            IsBroadcast = true,
            MessageType = "ProjectStart",
            ProjectId = project.Id,
            MemoryKey = "bmad.current-phase"
        });

        return project;
    }

    public async Task<Project> UpdateProjectAsync(Project project)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        project.UpdatedAt = DateTime.UtcNow;
        db.Projects.Update(project);
        await db.SaveChangesAsync();
        return project;
    }

    public async Task DeleteProjectAsync(int id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(id);
        if (project != null)
        {
            db.Projects.Remove(project);
            await db.SaveChangesAsync();
        }
    }

    public async Task AssignAgentAsync(int projectId, int agentId, string? role = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var existing = await db.ProjectAgentAssignments
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.AgentId == agentId);
        if (existing == null)
        {
            db.ProjectAgentAssignments.Add(new ProjectAgentAssignment
            {
                ProjectId = projectId,
                AgentId = agentId,
                Role = role
            });
            await db.SaveChangesAsync();
        }
    }

    public async Task RemoveAgentAsync(int projectId, int agentId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var assignment = await db.ProjectAgentAssignments
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.AgentId == agentId);
        if (assignment != null)
        {
            db.ProjectAgentAssignments.Remove(assignment);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Project> StartProjectAsync(int projectId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var project = await db.Projects
            .Include(p => p.LeadAgent)
            .Include(p => p.AgentAssignments).ThenInclude(a => a.Agent)
            .FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        project.Status = "Active";
        project.UpdatedAt = DateTime.UtcNow;

        // Ensure workspace folder exists (creates it if missing or previously deleted)
        await _workspace.EnsureProjectFolderAsync(project.Name, project.Id);

        // Update BMAD memory
        async Task UpsertMem(string key, string value, string category)
        {
            var existing = await db.BmadMemories.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.Key == key);
            var pmName = project.LeadAgent?.PersonaName ?? project.LeadAgent?.Name ?? "John";
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedByAgent = pmName;
            }
            else
            {
                db.BmadMemories.Add(new BmadMemory
                {
                    Key = key, Value = value, ProjectId = projectId,
                    Category = category, UpdatedByAgent = pmName,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await UpsertMem("project.status",    "Active",   "Config");
        await UpsertMem("bmad.workflow",      "Active",   "BMAD");
        await UpsertMem("bmad.current-phase", "Analysis", "BMAD");
        await UpsertMem("bmad.next-step",     "Mary: run BP (Brainstorming) → produce product-brief.md", "BMAD");

        // Resolve agents
        var assignedAgents = project.AgentAssignments.Select(a => a.Agent).ToList();
        var allAgents = await db.Agents.ToListAsync();
        Agent? FindAgent(AgentRole role) =>
            assignedAgents.FirstOrDefault(a => a.Role == role)
            ?? allAgents.FirstOrDefault(a => a.Role == role);

        int? AgentId(AgentRole role) => FindAgent(role)?.Id;

        const int backlogColumnId = 1;
        var now = DateTime.UtcNow;

        db.KanbanCards.AddRange(new[]
        {
            new KanbanCard
            {
                Title = $"[{project.Name}] Product Brief",
                Description = "BMAD Phase 1 — Analysis: brainstorm requirements and produce product-brief.md.",
                ColumnId = backlogColumnId, Priority = "High", StoryType = "Epic",
                Tags = "BMAD,Analysis,BP", ProjectId = projectId,
                AssignedAgentId = AgentId(AgentRole.Analyst),
                Order = 0, CreatedAt = now, UpdatedAt = now,
                AcceptanceCriteria = "- product-brief.md created in planning-artifacts/\n- All key requirements captured"
            },
            new KanbanCard
            {
                Title = $"[{project.Name}] Create PRD",
                Description = "BMAD Phase 2 — Planning: John creates the PRD from product-brief.md.",
                ColumnId = backlogColumnId, Priority = "High", StoryType = "Epic",
                Tags = "BMAD,Planning,CP", ProjectId = projectId,
                AssignedAgentId = AgentId(AgentRole.ProductManager),
                Order = 1, CreatedAt = now, UpdatedAt = now,
                AcceptanceCriteria = "- PRD.md created in planning-artifacts/\n- PRD validated (VP trigger)"
            },
            new KanbanCard
            {
                Title = $"[{project.Name}] UX Spec",
                Description = "BMAD Phase 2 — Planning: Sally creates the UX spec from PRD.",
                ColumnId = backlogColumnId, Priority = "Medium", StoryType = "Epic",
                Tags = "BMAD,Planning,CU", ProjectId = projectId,
                AssignedAgentId = AgentId(AgentRole.UxDesigner),
                Order = 2, CreatedAt = now, UpdatedAt = now,
                AcceptanceCriteria = "- ux-spec.md created in planning-artifacts/"
            },
            new KanbanCard
            {
                Title = $"[{project.Name}] Architecture",
                Description = "BMAD Phase 3 — Solutioning: Winston designs architecture and ADRs.",
                ColumnId = backlogColumnId, Priority = "High", StoryType = "Epic",
                Tags = "BMAD,Solutioning,CA", ProjectId = projectId,
                AssignedAgentId = AgentId(AgentRole.Architect),
                Order = 3, CreatedAt = now, UpdatedAt = now,
                AcceptanceCriteria = "- architecture.md created\n- Implementation Readiness Check (IR) passes"
            },
            new KanbanCard
            {
                Title = $"[{project.Name}] Sprint Planning",
                Description = "BMAD Phase 4 — Implementation: Bob breaks architecture into sprint stories.",
                ColumnId = backlogColumnId, Priority = "Medium", StoryType = "Epic",
                Tags = "BMAD,Implementation,SP", ProjectId = projectId,
                AssignedAgentId = AgentId(AgentRole.ScrumMaster),
                Order = 4, CreatedAt = now, UpdatedAt = now,
                AcceptanceCriteria = "- Stories created with status ready-for-dev\n- Sprint board populated"
            },
        });

        await db.SaveChangesAsync();

        var pm = FindAgent(AgentRole.ProductManager);

        // PM uses LLM to generate kickoff broadcast
        string broadcastContent;
        if (await _llmChat.IsAvailableAsync())
        {
            var pmPrompt = new List<OllamaChatMessage>
            {
                new("system", pm?.SystemPrompt ??
                    "You are John, a Product Manager for the BMAD framework with 8+ years B2B/consumer experience. " +
                    "You are relentlessly curious and MVP-focused. Keep responses concise and actionable."),
                new("user",
                    $"Project '{project.Name}' has just been ACTIVATED in our BMAD workflow system. " +
                    $"Project description: {(string.IsNullOrWhiteSpace(project.Description) ? "No description provided." : project.Description)} " +
                    $"Assigned team: {string.Join(", ", assignedAgents.Select(a => a.PersonaName ?? a.Name))}. " +
                    "Write a brief project kickoff announcement (3-4 sentences max) to the whole team. " +
                    "Mention Phase 1 Analysis is starting and what the team should focus on. Be energetic and direct.")
            };

            var llmReply = await _llmChat.ChatAsync(pmPrompt, pm);
            broadcastContent = string.IsNullOrWhiteSpace(llmReply) || llmReply.StartsWith("[LLM")
                ? FallbackBroadcast(project)
                : $"[{pm?.PersonaName ?? "PM"}]\n\n{llmReply}";
        }
        else
        {
            broadcastContent = FallbackBroadcast(project);
        }

        await _messageService.SendMessageAsync(new AgentMessage
        {
            Content = broadcastContent,
            SenderId = pm?.Id,
            ReceiverId = null,
            IsBroadcast = true,
            MessageType = "ProjectStart",
            ProjectId = projectId,
            MemoryKey = "bmad.current-phase"
        });

        // PM → Mary: LLM-generated handoff instructions
        var mary = FindAgent(AgentRole.Analyst);
        if (mary != null && pm != null && mary.Id != pm.Id)
        {
            string handoffContent;
            if (await _llmChat.IsAvailableAsync())
            {
                var handoffPrompt = new List<OllamaChatMessage>
                {
                    new("system", pm.SystemPrompt ??
                        "You are John, a Product Manager for the BMAD framework. Keep responses concise and actionable."),
                    new("user",
                        $"Write a direct handoff message to Mary (our Analyst) for project '{project.Name}'. " +
                        $"Project description: {(string.IsNullOrWhiteSpace(project.Description) ? "TBD" : project.Description)} " +
                        "Tell her to use trigger BP (Brainstorming) to start, what questions she should investigate, " +
                        "and what the product-brief.md must contain. Keep it under 5 bullet points.")
                };

                var handoffReply = await _llmChat.ChatAsync(handoffPrompt, pm);
                handoffContent = string.IsNullOrWhiteSpace(handoffReply) || handoffReply.StartsWith("[LLM")
                    ? FallbackHandoff(project)
                    : $"[{pm.PersonaName ?? "PM"}]\n\n{handoffReply}";
            }
            else
            {
                handoffContent = FallbackHandoff(project);
            }

            await _messageService.SendMessageAsync(new AgentMessage
            {
                Content = handoffContent,
                SenderId = pm.Id,
                ReceiverId = mary.Id,
                IsBroadcast = false,
                MessageType = "Handoff",
                ProjectId = projectId,
                MemoryKey = "bmad.next-step"
            });

            // Update Mary's status to Working
            var maryEntity = await db.Agents.FindAsync(mary.Id);
            if (maryEntity != null)
            {
                maryEntity.Status = AgentStatus.Working;
                maryEntity.UpdatedAt = DateTime.UtcNow;
            }
            // PM also working
            if (pm?.Id != null)
            {
                var pmEntity = await db.Agents.FindAsync(pm.Id);
                if (pmEntity != null)
                {
                    pmEntity.Status = AgentStatus.Working;
                    pmEntity.UpdatedAt = DateTime.UtcNow;
                }
            }
            await db.SaveChangesAsync();
        }

        return project;
    }

    public async Task<AgentMessage> SendPmMessageAsync(int projectId, string userMessage)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var project = await db.Projects
            .Include(p => p.LeadAgent)
            .Include(p => p.AgentAssignments).ThenInclude(a => a.Agent)
            .FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var allAgents = await db.Agents.ToListAsync();
        var pm = project.LeadAgent
                 ?? project.AgentAssignments.Select(a => a.Agent).FirstOrDefault(a => a.Role == AgentRole.ProductManager)
                 ?? allAgents.FirstOrDefault(a => a.Role == AgentRole.ProductManager);

        // Store the user's inbound message
        await _messageService.SendMessageAsync(new AgentMessage
        {
            Content = userMessage,
            SenderId = null,
            ReceiverId = pm?.Id,
            IsBroadcast = false,
            MessageType = "Request",
            ProjectId = projectId
        });

        // Build conversation context — load history BEFORE this turn (exclude the message we just saved)
        var recentMessages = await db.AgentMessages
            .Include(m => m.Sender)
            .Where(m => m.ProjectId == projectId && m.MessageType != "Request" ||
                        m.ProjectId == projectId && m.MessageType == "Request" && m.Content != userMessage)
            .OrderByDescending(m => m.SentAt)
            .Take(8)
            .ToListAsync();
        recentMessages.Reverse();

        string replyContent;
        if (await _llmChat.IsAvailableAsync())
        {
            var chatMessages = new List<OllamaChatMessage>
            {
                new("system",
                    (pm?.SystemPrompt ?? "You are John, a Product Manager for the BMAD framework.") +
                    $"\n\nProject: {project.Name}. Description: {project.Description ?? "N/A"}. " +
                    $"Status: {project.Status}. Current BMAD phase: {project.BmadPhase}. " +
                    "Respond naturally and concisely. Do not include role tags like [John] in your reply.")
            };

            // Map history: PM's own messages → assistant role; everything else → user role
            foreach (var msg in recentMessages)
            {
                var isFromPm = msg.SenderId == pm?.Id;
                chatMessages.Add(new(isFromPm ? "assistant" : "user", msg.Content));
            }

            chatMessages.Add(new("user", userMessage));

            var llmReply = await _llmChat.ChatAsync(chatMessages, pm);
            replyContent = string.IsNullOrWhiteSpace(llmReply) || llmReply.StartsWith("[LLM error")
                ? $"[PM offline] {llmReply}"
                : $"[{pm?.PersonaName ?? "PM"}]\n\n{llmReply}";
        }
        else
        {
            replyContent = $"[{pm?.PersonaName ?? "PM"}] LLM not enabled. Enable a provider in Settings → LLM Connections.";
        }

        var reply = await _messageService.SendMessageAsync(new AgentMessage
        {
            Content = replyContent,
            SenderId = pm?.Id,
            ReceiverId = null,
            IsBroadcast = false,
            MessageType = "Response",
            ProjectId = projectId
        });

        return reply;
    }

    private static string FallbackBroadcast(Project project) =>
        $"🚀 Project '{project.Name}' is now ACTIVE. BMAD task plan created — 5 epics added to Kanban Backlog. " +
        $"Phase 1 Analysis begins now. {(string.IsNullOrWhiteSpace(project.Description) ? "" : $"Goal: {project.Description}. ")}" +
        "Mary: please run BP (Brainstorming) to kick off requirements gathering. " +
        "All agents: check your assigned Kanban cards.";

    private static string FallbackHandoff(Project project) =>
        $"Mary, project '{project.Name}' is ready for Phase 1. Please run trigger BP (Brainstorming) to begin. " +
        $"Goal: {(string.IsNullOrWhiteSpace(project.Description) ? "see project context" : project.Description)}. " +
        "Produce product-brief.md in planning-artifacts/ covering: goals, user needs, constraints, success metrics.";
}
