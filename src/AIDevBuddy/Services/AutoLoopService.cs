using System.Text.RegularExpressions;
using AIDevBuddy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIDevBuddy.Services;

/// <summary>
/// Drives the BMAD AutoLoop: autonomous agent turns through
/// Analysis → Planning → Solutioning → Implementation → Documentation → Complete.
/// Registered as singleton + IHostedService so loops survive Blazor component
/// disposal and auto-resume on app restart.
/// </summary>
public sealed class AutoLoopService : IAutoLoopService, IHostedService, IDisposable
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoLoopService> _logger;
    private readonly IWorkspaceService _workspace;

    // projectId → CancellationTokenSource for the running loop task
    private readonly Dictionary<int, CancellationTokenSource> _loops = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action<int>? ProjectUpdated;

    public AutoLoopService(
        IDbContextFactory<AppDbContext> dbFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<AutoLoopService> logger,
        IWorkspaceService workspace)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _workspace = workspace;
    }

    // ── IHostedService ───────────────────────────────────────────────────────

    public async Task StartAsync(CancellationToken ct)
    {
        // Auto-resume any projects that were Running when the app last stopped
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var running = await db.Projects
            .Where(p => p.AutoLoop && p.AutoLoopStatus == "Running")
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var id in running)
            await StartLoopAsync(id);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            foreach (var cts in _loops.Values)
                cts.Cancel();
            _loops.Clear();
        }
        finally { _lock.Release(); }
    }

    // ── IAutoLoopService ─────────────────────────────────────────────────────

    public async Task StartLoopAsync(int projectId)
    {
        await _lock.WaitAsync();
        try
        {
            // Cancel any existing loop for this project
            if (_loops.TryGetValue(projectId, out var old))
            {
                old.Cancel();
                _loops.Remove(projectId);
            }

            var cts = new CancellationTokenSource();
            _loops[projectId] = cts;
        }
        finally { _lock.Release(); }

        await SetDbStatusAsync(projectId, "Running");
        _ = Task.Run(() => LoopAsync(projectId, _loops[projectId].Token));
    }

    public async Task PauseLoopAsync(int projectId)
    {
        await CancelLoopInternalAsync(projectId);
        await SetDbStatusAsync(projectId, "Paused");
    }

    public async Task ResumeLoopAsync(int projectId)
    {
        await StartLoopAsync(projectId);
    }

    public async Task StopLoopAsync(int projectId)
    {
        await CancelLoopInternalAsync(projectId);
        await SetDbStatusAsync(projectId, "Idle");
    }

    public bool IsRunning(int projectId)
    {
        _lock.Wait();
        try { return _loops.ContainsKey(projectId); }
        finally { _lock.Release(); }
    }

    public string GetLoopStatus(int projectId) =>
        IsRunning(projectId) ? "Running" : "Stopped";

    // ── Loop core ────────────────────────────────────────────────────────────

    private async Task LoopAsync(int projectId, CancellationToken ct)
    {
        _logger.LogInformation("AutoLoop started for project {ProjectId}", projectId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var (cont, delayMs) = await RunAgentTurnAsync(projectId, ct);
                if (!cont) break;
                await Task.Delay(delayMs, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoLoop error for project {ProjectId}", projectId);
                await SetDbStatusAsync(projectId, "Error");
                await BroadcastAsync(projectId, null,
                    $"⚠️ AutoLoop paused due to error: {ex.Message}\nFix the issue and Resume.", "Alert");
                ProjectUpdated?.Invoke(projectId);
                break;
            }
        }

        _logger.LogInformation("AutoLoop stopped for project {ProjectId}", projectId);
    }

    private async Task<(bool cont, int delayMs)> RunAgentTurnAsync(int projectId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var project = await db.Projects
            .Include(p => p.AgentAssignments).ThenInclude(a => a.Agent)
            .Include(p => p.LeadAgent)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null) return (false, 0);
        if (!project.AutoLoop) return (false, 0);
        if (project.AutoLoopStatus is "Paused" or "Complete" or "Error") return (false, 0);
        if (project.BmadPhase == "Complete" || project.Status == "Complete")
        {
            await FinalizeProjectAsync(db, project, ct);
            return (false, 0);
        }

        // Verify at least one LLM provider is enabled
        var anyLlm = await db.LlmSettings.AnyAsync(s => s.IsEnabled, ct);
        if (!anyLlm)
        {
            await BroadcastAsync(projectId, null,
                "⚠️ AutoLoop paused: no LLM provider is enabled. Enable one in Settings → LLM Connections.", "Alert");
            await SetDbStatusAsync(projectId, "Paused");
            ProjectUpdated?.Invoke(projectId);
            return (false, 0);
        }

        // Determine current work item
        var (agent, card, instruction) = await DetermineWorkItemAsync(db, project, ct);

        if (agent == null)
        {
            // No agent for current phase → advance phase
            await AdvancePhaseAsync(db, project, ct);
            return (true, 1500);
        }

        // Enforce per-phase turn limit (prevents infinite loops)
        var turnKey = $"autoloop.turns.{project.BmadPhase}";
        var turns = int.Parse(await GetMemValAsync(db, projectId, turnKey, ct) ?? "0");
        if (turns >= 20)
        {
            await BroadcastAsync(projectId, agent.Id,
                $"⚠️ AutoLoop paused: {project.BmadPhase} phase exceeded 20 turns without completing. " +
                "Resume after investigating.", "Alert");
            await SetDbStatusAsync(projectId, "Paused");
            ProjectUpdated?.Invoke(projectId);
            return (false, 0);
        }
        await UpsertMemAsync(db, projectId, turnKey, (turns + 1).ToString(), "BMAD", "AutoLoop", ct);

        // Mark agent as Working + emit a "thinking" indicator in the feed
        var agentRow = await db.Agents.FindAsync(new object[] { agent.Id }, ct);
        if (agentRow != null) { agentRow.Status = AgentStatus.Working; agentRow.UpdatedAt = DateTime.UtcNow; }

        db.AgentMessages.Add(new AgentMessage
        {
            Content     = $"🤔 Thinking… [{project.BmadPhase} phase]{(card != null ? $" — {card.Title}" : "")}",
            SenderId    = agent.Id,
            IsBroadcast = false,
            MessageType = "Thinking",
            ProjectId   = projectId,
            SentAt      = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        ProjectUpdated?.Invoke(projectId); // refresh UI immediately so status badge shows Working

        // Gather context
        var artifacts = await BuildArtifactsContextAsync(db, projectId, ct);
        var recentMsgs = await db.AgentMessages
            .Include(m => m.Sender)
            .Where(m => m.ProjectId == projectId)
            .OrderByDescending(m => m.SentAt)
            .Take(6)
            .ToListAsync(ct);
        recentMsgs.Reverse();

        var systemPrompt = BuildSystemPrompt(agent);
        var userPrompt   = BuildUserPrompt(project, agent, card, instruction, artifacts, recentMsgs);

        await db.SaveChangesAsync(ct);

        // Call LLM via provider-aware router
        string llmResponse;
        using (var scope = _scopeFactory.CreateScope())
        {
            var chat = scope.ServiceProvider.GetRequiredService<ILlmChatService>();
            llmResponse = await chat.ChatAsync(
                new List<OllamaChatMessage>
                {
                    new("system", systemPrompt),
                    new("user",   userPrompt)
                }, agent);
        }

        if (string.IsNullOrWhiteSpace(llmResponse) || llmResponse.StartsWith("[LLM error"))
        {
            _logger.LogWarning("LLM error for project {ProjectId}: {Err}", projectId, llmResponse);
            await SetDbStatusAsync(projectId, "Error");
            await BroadcastAsync(projectId, agent.Id, $"⚠️ LLM call failed: {llmResponse}", "Alert");
            ProjectUpdated?.Invoke(projectId);
            return (false, 0);
        }

        // Store the response as an agent message (strip structural tags for readability)
        await using var db2 = await _dbFactory.CreateDbContextAsync(ct);
        db2.AgentMessages.Add(new AgentMessage
        {
            Content     = StripTags(llmResponse),
            SenderId    = agent.Id,
            IsBroadcast = false,
            MessageType = "AutoLoop",
            ProjectId   = projectId,
            SentAt      = DateTime.UtcNow
        });
        await db2.SaveChangesAsync(ct);

        // Process structured output tags
        await ProcessResponseAsync(db2, project, agent, card, llmResponse, ct);

        // Reset agent status to Idle now that the turn is complete
        await using var dbIdle = await _dbFactory.CreateDbContextAsync(ct);
        var idleRow = await dbIdle.Agents.FindAsync(new object[] { agent.Id }, ct);
        if (idleRow != null && idleRow.Status == AgentStatus.Working)
        {
            idleRow.Status    = AgentStatus.Idle;
            idleRow.UpdatedAt = DateTime.UtcNow;
            await dbIdle.SaveChangesAsync(ct);
        }

        ProjectUpdated?.Invoke(projectId);

        return (true, project.AutoLoopIntervalSeconds * 1000);
    }

    // ── Work-item determination ───────────────────────────────────────────────

    private async Task<(Agent? agent, KanbanCard? card, string instruction)> DetermineWorkItemAsync(
        AppDbContext db, Project project, CancellationToken ct)
    {
        var assigned = project.AgentAssignments.Select(a => a.Agent).ToList();

        // Prefer project-assigned agent; fall back to any seeded global agent with that role
        async Task<Agent?> PickAsync(AgentRole role) =>
            assigned.FirstOrDefault(a => a.Role == role)
            ?? await db.Agents.FirstOrDefaultAsync(a => a.Role == role, ct);

        int pid = project.Id;

        switch (project.BmadPhase)
        {
            case "Analysis":
            {
                if (await GetMemValAsync(db, pid, "autoloop.analysis-done", ct) == "true")
                    return (null, null, "");
                var agent = await PickAsync(AgentRole.Analyst);
                if (agent == null) return (null, null, ""); // no analyst anywhere → skip
                var card = await FindCardAsync(db, pid, "Product Brief", ct);
                return (agent, card,
                    "Create a comprehensive product-brief for this project based on the description. " +
                    "Save it as [ARTIFACT: product-brief]...[/ARTIFACT]. " +
                    "When done emit [PHASE_COMPLETE].");
            }

            case "Planning":
            {
                var step = await GetMemValAsync(db, pid, "autoloop.planning-step", ct) ?? "PRD";
                if (step == "done") return (null, null, "");

                if (step == "PRD")
                {
                    var pm = await PickAsync(AgentRole.ProductManager);
                    if (pm == null)
                    {
                        await UpsertMemAsync(db, pid, "autoloop.planning-step", "UXSpec", "BMAD", "AutoLoop", ct);
                        return (null, null, "");
                    }
                    var card = await FindCardAsync(db, pid, "Create PRD", ct);
                    return (pm, card,
                        "Create a detailed PRD (Product Requirements Document) from the product-brief artifact. " +
                        "Save as [ARTIFACT: prd]...[/ARTIFACT]. " +
                        "When done, emit [HANDOFF: Sally | Please create the UX spec from the PRD.].");
                }
                else // UXSpec
                {
                    var ux = await PickAsync(AgentRole.UxDesigner);
                    if (ux == null)
                    {
                        await UpsertMemAsync(db, pid, "autoloop.planning-step", "done", "BMAD", "AutoLoop", ct);
                        return (null, null, "");
                    }
                    var card = await FindCardAsync(db, pid, "UX Spec", ct);
                    return (ux, card,
                        "Create a UX specification from the PRD artifact. " +
                        "Cover user flows, wireframe descriptions, and interaction patterns. " +
                        "Save as [ARTIFACT: ux-spec]...[/ARTIFACT]. " +
                        "When done emit [PHASE_COMPLETE].");
                }
            }

            case "Solutioning":
            {
                if (await GetMemValAsync(db, pid, "autoloop.solutioning-done", ct) == "true")
                    return (null, null, "");
                var arch = await PickAsync(AgentRole.Architect);
                if (arch == null) return (null, null, "");
                var card = await FindCardAsync(db, pid, "Architecture", ct);
                return (arch, card,
                    "Design the technical architecture based on the PRD and UX spec artifacts. " +
                    "Include component design, data models, API contracts, and at least 2 ADRs. " +
                    "Save as [ARTIFACT: architecture]...[/ARTIFACT]. " +
                    "When done emit [PHASE_COMPLETE].");
            }

            case "Implementation":
            {
                var step = await GetMemValAsync(db, pid, "autoloop.impl-step", ct) ?? "SprintPlanning";
                if (step == "done") return (null, null, "");

                if (step == "SprintPlanning")
                {
                    var sm = await PickAsync(AgentRole.ScrumMaster);
                    if (sm == null)
                    {
                        await UpsertMemAsync(db, pid, "autoloop.impl-step", "Development", "BMAD", "AutoLoop", ct);
                        return (null, null, "");
                    }
                    var card = await FindCardAsync(db, pid, "Sprint Planning", ct);
                    return (sm, card,
                        "Break the architecture into 3–6 focused implementation stories using " +
                        "[STORY: Title | Description | Acceptance Criteria] tags (one per line). " +
                        "Stories should be small, independently deliverable. " +
                        "When done emit [HANDOFF: Amelia | Implement the stories in order, starting from the first.].");
                }

                if (step == "Development")
                {
                    var dev = await PickAsync(AgentRole.Developer);
                    if (dev == null)
                    {
                        var qaAgent = await PickAsync(AgentRole.QA);
                        await UpsertMemAsync(db, pid, "autoloop.impl-step",
                            qaAgent != null ? "QA" : "done", "BMAD", "AutoLoop", ct);
                        return (null, null, "");
                    }

                    var next = await db.KanbanCards
                        .Include(c => c.Column)
                        .Where(c => c.ProjectId == pid && c.StoryType == "Story" &&
                                    c.Column.Status == KanbanStatus.ReadyForDev)
                        .OrderBy(c => c.Order)
                        .FirstOrDefaultAsync(ct);

                    if (next == null)
                    {
                        var wip = await db.KanbanCards
                            .Include(c => c.Column)
                            .Where(c => c.ProjectId == pid && c.StoryType == "Story" &&
                                        c.Column.Status == KanbanStatus.InProgress)
                            .FirstOrDefaultAsync(ct);
                        if (wip != null)
                            return (dev, wip, "Continue implementing this story. Write tests + implementation. Emit [CARD_DONE: title] when complete.");

                        var qaAgent = await PickAsync(AgentRole.QA);
                        await UpsertMemAsync(db, pid, "autoloop.impl-step",
                            qaAgent != null ? "QA" : "done", "BMAD", "AutoLoop", ct);
                        return (null, null, "");
                    }

                    return (dev, next,
                        "Implement this story following TDD: write tests first, then code. " +
                        "Output the full implementation plan, test cases, and representative code. " +
                        "Emit [CARD_DONE: exact-story-title] when complete.");
                }

                if (step == "QA")
                {
                    var qa = await PickAsync(AgentRole.QA);
                    if (qa == null)
                    {
                        await UpsertMemAsync(db, pid, "autoloop.impl-step", "done", "BMAD", "AutoLoop", ct);
                        return (null, null, "");
                    }

                    var review = await db.KanbanCards
                        .Include(c => c.Column)
                        .Where(c => c.ProjectId == pid && c.StoryType == "Story" &&
                                    c.Column.Status == KanbanStatus.Review)
                        .OrderBy(c => c.Order)
                        .FirstOrDefaultAsync(ct);

                    if (review == null)
                    {
                        await UpsertMemAsync(db, pid, "autoloop.impl-step", "done", "BMAD", "AutoLoop", ct);
                        return (null, null, "");
                    }

                    return (qa, review,
                        "Review and test this story against its acceptance criteria. " +
                        "Run through: Blind Hunter (quality gaps), Edge Case Hunter, Acceptance Auditor. " +
                        "Emit [TEST_RESULT: exact-story-title | PASS | notes] or [TEST_RESULT: exact-story-title | FAIL | what-is-missing].");
                }

                return (null, null, "");
            }

            case "Documentation":
            {
                if (await GetMemValAsync(db, pid, "autoloop.docs-done", ct) == "true")
                    return (null, null, "");
                var writer = await PickAsync(AgentRole.TechWriter);
                if (writer == null) return (null, null, "");
                return (writer, null,
                    "Create comprehensive technical documentation for this project based on all produced artifacts. " +
                    "Include: API reference, user guide, architecture overview, and a getting-started section. " +
                    "Save as [ARTIFACT: documentation]...[/ARTIFACT]. " +
                    "When done emit [PHASE_COMPLETE].");
            }

            default:
                return (null, null, "");
        }
    }

    // ── Response processing ───────────────────────────────────────────────────

    private async Task ProcessResponseAsync(
        AppDbContext db, Project project, Agent agent, KanbanCard? card,
        string response, CancellationToken ct)
    {
        int pid = project.Id;
        var agentName = agent.PersonaName ?? agent.Name;

        // 1 — Artifacts
        var artifactRx = new Regex(@"\[ARTIFACT:\s*([^\]]+)\](.*?)\[/ARTIFACT\]",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match m in artifactRx.Matches(response))
        {
            var name    = m.Groups[1].Value.Trim().ToLowerInvariant().Replace(' ', '-');
            var content = m.Groups[2].Value.Trim();
            await UpsertMemAsync(db, pid, $"artifacts.{name}",         "produced", "Artifacts", agentName, ct);
            await UpsertMemAsync(db, pid, $"artifacts.{name}.content", content,    "Artifacts", agentName, ct);

            // Persist artifact to project workspace folder
            await _workspace.WriteArtifactAsync(project.Name, pid, name, content);

            var artifactPath = Path.Combine(
                _workspace.GetProjectFolder(project.Name, pid), "artifacts", $"{name}.md");
            db.AgentMessages.Add(new AgentMessage
            {
                Content = $"📄 Artifact saved: **{name}** ({content.Length} chars) → `{artifactPath}`",
                SenderId = agent.Id, IsBroadcast = true,
                MessageType = "MemoryUpdate", ProjectId = pid, SentAt = DateTime.UtcNow
            });
        }

        // 2 — Memory updates
        var memRx = new Regex(@"\[MEMORY:\s*([^=\]\r\n]+)=([^\]\r\n]+)\]", RegexOptions.IgnoreCase);
        foreach (Match m in memRx.Matches(response))
            await UpsertMemAsync(db, pid, m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim(),
                "General", agentName, ct);

        // 3 — Sprint stories (ScrumMaster)
        var storyRx = new Regex(@"\[STORY:\s*([^\|]+)\|([^\|]+)\|([^\]]+)\]", RegexOptions.IgnoreCase);
        var devAgent = project.AgentAssignments.Select(a => a.Agent)
                              .FirstOrDefault(a => a.Role == AgentRole.Developer);
        int storyOrder = 200;
        foreach (Match m in storyRx.Matches(response))
        {
            var title = m.Groups[1].Value.Trim();
            var desc  = m.Groups[2].Value.Trim();
            var ac    = m.Groups[3].Value.Trim();
            db.KanbanCards.Add(new KanbanCard
            {
                Title = title, Description = desc, AcceptanceCriteria = ac,
                ColumnId = 2, // ReadyForDev
                StoryType = "Story", Priority = "Medium",
                Tags = "BMAD,AutoLoop", ProjectId = pid,
                AssignedAgentId = devAgent?.Id,
                Order = storyOrder++,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }

        // 4 — Card done (Developer marks implementation complete → moves to Review)
        var cardDoneRx = new Regex(@"\[CARD_DONE:\s*([^\]]+)\]", RegexOptions.IgnoreCase);
        foreach (Match m in cardDoneRx.Matches(response))
        {
            var title = m.Groups[1].Value.Trim();
            var target = await db.KanbanCards
                .Where(c => c.ProjectId == pid && c.Title.Contains(title))
                .FirstOrDefaultAsync(ct);
            if (target != null)
            {
                // Developer → Review; QA won't use CARD_DONE
                target.ColumnId = 4; // Review
                target.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 5 — QA test results
        var testRx = new Regex(@"\[TEST_RESULT:\s*([^\|]+)\|(PASS|FAIL)\|([^\]]+)\]",
            RegexOptions.IgnoreCase);
        foreach (Match m in testRx.Matches(response))
        {
            var title  = m.Groups[1].Value.Trim();
            var result = m.Groups[2].Value.Trim().ToUpper();
            var notes  = m.Groups[3].Value.Trim();
            var target = await db.KanbanCards
                .Where(c => c.ProjectId == pid && c.Title.Contains(title))
                .FirstOrDefaultAsync(ct);
            if (target != null)
            {
                if (result == "PASS")
                    target.ColumnId = 5; // Done
                // FAIL: stays in Review — QA next turn will re-test
                target.Description = (target.Description ?? "") + $"\n\n[QA {result}] {notes}";
                target.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 6 — Handoff (updates planning/impl sub-step)
        var handoffMatch = Regex.Match(response,
            @"\[HANDOFF:\s*([^\|]+)\|([^\]]+)\]", RegexOptions.IgnoreCase);
        if (handoffMatch.Success)
        {
            var target       = handoffMatch.Groups[1].Value.Trim();
            var instructions = handoffMatch.Groups[2].Value.Trim();

            var targetAgent = project.AgentAssignments.Select(a => a.Agent)
                .FirstOrDefault(a =>
                    (a.PersonaName != null && target.Contains(a.PersonaName, StringComparison.OrdinalIgnoreCase)) ||
                    target.Contains(a.Name, StringComparison.OrdinalIgnoreCase));

            db.AgentMessages.Add(new AgentMessage
            {
                Content = $"[Handoff from {agentName} → {target}]\n{instructions}",
                SenderId = agent.Id, ReceiverId = targetAgent?.Id,
                MessageType = "Handoff", ProjectId = pid, SentAt = DateTime.UtcNow
            });

            // Update sub-step state based on who is being handed to
            var low = target.ToLowerInvariant();
            if (low.Contains("sally") || low.Contains("ux") || low.Contains("designer"))
                await UpsertMemAsync(db, pid, "autoloop.planning-step", "UXSpec", "BMAD", "AutoLoop", ct);
            if (low.Contains("amelia") || low.Contains("dev"))
                await UpsertMemAsync(db, pid, "autoloop.impl-step", "Development", "BMAD", "AutoLoop", ct);
            if (low.Contains("quinn") || low.Contains("qa"))
                await UpsertMemAsync(db, pid, "autoloop.impl-step", "QA", "BMAD", "AutoLoop", ct);
            if (low.Contains("paige") || low.Contains("writer") || low.Contains("doc"))
                await UpsertMemAsync(db, pid, "autoloop.docs-step", "Documentation", "BMAD", "AutoLoop", ct);
        }

        // 7 — Blocked: route a question to another agent (fire one helper turn)
        var blockedMatch = Regex.Match(response,
            @"\[BLOCKED:\s*([^\|]+)\|([^\]]+)\]", RegexOptions.IgnoreCase);
        if (blockedMatch.Success)
        {
            var targetName = blockedMatch.Groups[1].Value.Trim();
            var question   = blockedMatch.Groups[2].Value.Trim();
            var helper = project.AgentAssignments.Select(a => a.Agent)
                .FirstOrDefault(a =>
                    (a.PersonaName != null && targetName.Contains(a.PersonaName, StringComparison.OrdinalIgnoreCase)) ||
                    targetName.Contains(a.Name, StringComparison.OrdinalIgnoreCase));

            db.AgentMessages.Add(new AgentMessage
            {
                Content = $"[{agentName} is blocked]\n{question}",
                SenderId = agent.Id, ReceiverId = helper?.Id,
                MessageType = "Request", ProjectId = pid, SentAt = DateTime.UtcNow
            });

            if (helper != null)
                await RunHelperTurnAsync(project, helper, agent, question, ct);
        }

        await db.SaveChangesAsync(ct);

        // 8 — Phase complete
        if (response.Contains("[PHASE_COMPLETE]", StringComparison.OrdinalIgnoreCase))
        {
            // Reset turn counter for completed phase
            await UpsertMemAsync(db, pid, $"autoloop.turns.{project.BmadPhase}", "0", "BMAD", "AutoLoop", ct);

            // Load fresh project for phase advance
            await using var db3 = await _dbFactory.CreateDbContextAsync(ct);
            var fresh = await db3.Projects
                .Include(p => p.AgentAssignments).ThenInclude(a => a.Agent)
                .FirstOrDefaultAsync(p => p.Id == pid, ct);
            if (fresh != null)
                await AdvancePhaseAsync(db3, fresh, ct);
        }
        else if (card != null)
        {
            // Move epic/task card to InProgress if still in Backlog/ReadyForDev
            await using var dbCard = await _dbFactory.CreateDbContextAsync(ct);
            var c = await dbCard.KanbanCards.FindAsync(new object[] { card.Id }, ct);
            if (c != null && (c.ColumnId == 1 || c.ColumnId == 2))
            {
                c.ColumnId = 3; // InProgress
                c.UpdatedAt = DateTime.UtcNow;
                await dbCard.SaveChangesAsync(ct);
            }
        }
    }

    // ── Phase advancement ─────────────────────────────────────────────────────

    private async Task AdvancePhaseAsync(AppDbContext db, Project project, CancellationToken ct)
    {
        var current  = project.BmadPhase;
        var assigned = project.AgentAssignments.Select(a => a.Agent).ToList();

        // Mark phase-specific epic cards as Done
        await MarkEpicDoneAsync(db, project.Id, current, ct);

        string next = current switch
        {
            "Analysis"       => "Planning",
            "Planning"       => "Solutioning",
            "Solutioning"    => "Implementation",
            "Implementation" => assigned.Any(a => a.Role == AgentRole.TechWriter)
                                    ? "Documentation" : "Complete",
            "Documentation"  => "Complete",
            _                => "Complete"
        };

        if (next == "Complete")
        {
            await FinalizeProjectAsync(db, project, ct);
            return;
        }

        project.BmadPhase  = next;
        project.UpdatedAt  = DateTime.UtcNow;
        await UpsertMemAsync(db, project.Id, "bmad.current-phase", next, "BMAD", "AutoLoop", ct);
        await UpsertMemAsync(db, project.Id, $"autoloop.turns.{next}", "0", "BMAD", "AutoLoop", ct);
        db.Projects.Update(project);

        db.AgentMessages.Add(new AgentMessage
        {
            Content     = $"📋 Phase **{current}** complete → advancing to **{next}**",
            IsBroadcast = true,
            MessageType = "PhaseTransition",
            ProjectId   = project.Id,
            SentAt      = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        ProjectUpdated?.Invoke(project.Id);
    }

    private async Task FinalizeProjectAsync(AppDbContext db, Project project, CancellationToken ct)
    {
        project.Status         = "Complete";
        project.BmadPhase      = "Complete";
        project.AutoLoopStatus = "Complete";
        project.UpdatedAt      = DateTime.UtcNow;
        db.Projects.Update(project);

        db.AgentMessages.Add(new AgentMessage
        {
            Content     = "🎉 **Project complete!** All BMAD phases finished. Documentation and testing done. Great work team!",
            IsBroadcast = true,
            MessageType = "Broadcast",
            ProjectId   = project.Id,
            SentAt      = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);

        await _lock.WaitAsync(ct);
        try
        {
            if (_loops.TryGetValue(project.Id, out var cts))
            {
                cts.Cancel();
                _loops.Remove(project.Id);
            }
        }
        finally { _lock.Release(); }

        ProjectUpdated?.Invoke(project.Id);
    }

    private async Task MarkEpicDoneAsync(AppDbContext db, int pid, string phase, CancellationToken ct)
    {
        var keywords = phase switch
        {
            "Analysis"    => new[] { "Product Brief" },
            "Planning"    => new[] { "Create PRD", "UX Spec" },
            "Solutioning" => new[] { "Architecture" },
            "Implementation" => new[] { "Sprint Planning" },
            _ => Array.Empty<string>()
        };

        foreach (var kw in keywords)
        {
            var c = await db.KanbanCards
                .FirstOrDefaultAsync(x => x.ProjectId == pid && x.Title.Contains(kw), ct);
            if (c != null && c.ColumnId != 5)
            {
                c.ColumnId  = 5; // Done
                c.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    // ── Helper-agent turn (when blocked) ─────────────────────────────────────

    private async Task RunHelperTurnAsync(
        Project project, Agent helper, Agent requester, string question, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var sys    = helper.SystemPrompt ?? $"You are {helper.PersonaName}, a BMAD agent.";
        var prompt = $"{requester.PersonaName ?? requester.Name} asked you: {question}\n\nAnswer concisely so they can continue their work.";

        string reply;
        using (var scope = _scopeFactory.CreateScope())
        {
            var chat = scope.ServiceProvider.GetRequiredService<ILlmChatService>();
            reply = await chat.ChatAsync(
                new List<OllamaChatMessage>
                {
                    new("system", sys),
                    new("user",   prompt)
                }, helper);
        }

        if (string.IsNullOrWhiteSpace(reply) || reply.StartsWith("[LLM error")) return;

        db.AgentMessages.Add(new AgentMessage
        {
            Content     = $"[{helper.PersonaName ?? helper.Name} answers {requester.PersonaName}]\n\n{reply}",
            SenderId    = helper.Id,
            ReceiverId  = requester.Id,
            MessageType = "Response",
            ProjectId   = project.Id,
            SentAt      = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    // ── Prompt builders ───────────────────────────────────────────────────────

    private static string BuildSystemPrompt(Agent agent)
    {
        var persona = agent.SystemPrompt
                      ?? $"You are {agent.PersonaName ?? agent.Name}, a BMAD agent with role {agent.Role}.";

        return $"""
{persona}

=== BMAD AUTOLOOP MODE ===
You are running autonomously. You MUST use structured output tags — these are parsed by the system.

AVAILABLE TAGS (use all that apply):

[ARTIFACT: name]
...full content...
[/ARTIFACT]
→ Saves a document/code artifact. Use descriptive names like: product-brief, prd, ux-spec, architecture, documentation.

[MEMORY: key=value]
→ Updates project state (key and value on same line, no newlines in value).

[STORY: Title | Description | Acceptance Criteria]
→ (ScrumMaster only) Creates a Kanban story card. One story per [STORY:] tag.

[CARD_DONE: exact-story-title]
→ (Developer only) Marks a story implementation complete → moves to QA review.

[TEST_RESULT: exact-story-title | PASS | notes]
[TEST_RESULT: exact-story-title | FAIL | what-needs-fixing]
→ (QA only) Records test result.

[HANDOFF: AgentPersonaName | instructions for them]
→ Passes work to another agent (Mary, John, Sally, Winston, Bob, Amelia, Quinn, Paige).

[BLOCKED: AgentPersonaName | your specific question]
→ Requests help from another agent before you can continue.

[PHASE_COMPLETE]
→ Signals your phase/sub-task is fully done. Use this when your deliverable is complete.

RULES:
- Never ask the user for input. Work autonomously.
- Always produce at least one [ARTIFACT:] or [PHASE_COMPLETE].
- Keep artifact content thorough but focused.
- Do not add the tags to prose descriptions — only use them in the structured positions shown above.
""";
    }

    private static string BuildUserPrompt(
        Project project, Agent agent, KanbanCard? card, string instruction,
        string artifactsContext, List<AgentMessage> recentMsgs)
    {
        var msgHistory = recentMsgs.Count > 0
            ? string.Join("\n", recentMsgs.Select(m =>
                $"[{m.Sender?.PersonaName ?? m.Sender?.Name ?? "User"}] {m.Content[..Math.Min(300, m.Content.Length)]}"))
            : "(no messages yet)";

        var cardSection = card != null
            ? $"""
CURRENT TASK CARD: {card.Title}
{card.Description ?? ""}
{(string.IsNullOrEmpty(card.AcceptanceCriteria) ? "" : $"Acceptance Criteria:\n{card.AcceptanceCriteria}")}
"""
            : "CURRENT TASK: General phase deliverable (no specific card)";

        return $"""
PROJECT: {project.Name}
DESCRIPTION: {project.Description ?? "(no description provided)"}
CURRENT BMAD PHASE: {project.BmadPhase}
YOUR ROLE: {agent.Role} ({agent.PersonaName ?? agent.Name})

{cardSection}

AVAILABLE ARTIFACTS:
{artifactsContext}

RECENT PROJECT MESSAGES:
{msgHistory}

YOUR INSTRUCTION: {instruction}

Please proceed with your work now.
""";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> BuildArtifactsContextAsync(AppDbContext db, int pid, CancellationToken ct)
    {
        var arts = await db.BmadMemories
            .Where(m => m.ProjectId == pid && m.Category == "Artifacts" &&
                        m.Key.EndsWith(".content"))
            .OrderBy(m => m.Key)
            .ToListAsync(ct);

        if (!arts.Any()) return "(no artifacts yet)";

        return string.Join("\n\n", arts.Select(a =>
        {
            var shortKey = a.Key.Replace(".content", "");
            var preview  = a.Value.Length > 600
                ? a.Value[..600] + "\n... (truncated)"
                : a.Value;
            return $"--- {shortKey} ---\n{preview}";
        }));
    }

    private async Task UpsertMemAsync(AppDbContext db, int pid, string key, string value,
        string category, string agent, CancellationToken ct = default)
    {
        var existing = await db.BmadMemories
            .FirstOrDefaultAsync(m => m.ProjectId == pid && m.Key == key, ct);
        var now = DateTime.UtcNow;
        if (existing != null)
        {
            existing.Value = value; existing.UpdatedAt = now; existing.UpdatedByAgent = agent;
        }
        else
        {
            db.BmadMemories.Add(new BmadMemory
            {
                Key = key, Value = value, ProjectId = pid,
                Category = category, UpdatedByAgent = agent,
                CreatedAt = now, UpdatedAt = now
            });
        }
    }

    private async Task<string?> GetMemValAsync(AppDbContext db, int pid, string key, CancellationToken ct)
    {
        var m = await db.BmadMemories
            .FirstOrDefaultAsync(x => x.ProjectId == pid && x.Key == key, ct);
        return m?.Value;
    }

    private async Task<KanbanCard?> FindCardAsync(AppDbContext db, int pid, string titleFragment, CancellationToken ct)
        => await db.KanbanCards
            .FirstOrDefaultAsync(c => c.ProjectId == pid && c.Title.Contains(titleFragment), ct);

    private async Task SetDbStatusAsync(int projectId, string status)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var p = await db.Projects.FindAsync(projectId);
            if (p != null)
            {
                p.AutoLoopStatus = status;
                p.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set AutoLoopStatus for project {ProjectId}", projectId);
        }
    }

    private async Task CancelLoopInternalAsync(int projectId)
    {
        await _lock.WaitAsync();
        try
        {
            if (_loops.TryGetValue(projectId, out var cts))
            {
                cts.Cancel();
                _loops.Remove(projectId);
            }
        }
        finally { _lock.Release(); }
    }

    private async Task BroadcastAsync(int pid, int? senderId, string content, string type)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.AgentMessages.Add(new AgentMessage
            {
                Content = content, SenderId = senderId,
                IsBroadcast = true, MessageType = type,
                ProjectId = pid, SentAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast message for project {ProjectId}", pid);
        }
    }

    /// <summary>Removes structured AutoLoop tags from text shown to users.</summary>
    private static string StripTags(string text)
    {
        text = Regex.Replace(text, @"\[ARTIFACT:[^\]]*\](.*?)\[/ARTIFACT\]",
            m => $"📄 [artifact: {m.Value.Length} chars saved]",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[(MEMORY|HANDOFF|BLOCKED|STORY|CARD_DONE|TEST_RESULT|PHASE_COMPLETE)[^\]]*\]",
            "", RegexOptions.IgnoreCase);
        return text.Trim();
    }

    public void Dispose()
    {
        foreach (var cts in _loops.Values) cts.Dispose();
        _lock.Dispose();
    }
}
