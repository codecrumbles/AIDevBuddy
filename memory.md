# AIDevBuddy - Implementation Memory

## Architecture Decisions
- Use IDbContextFactory for thread-safe EF Core access in Blazor Server
- All services registered as Scoped
- AddRadzenComponents() in Program.cs for Radzen DI
- SQLite stored in LocalApplicationData/AIDevBuddy/

## Key Files
- Data/Models.cs - All entity models and enums
- Data/AppDbContext.cs - EF Core context with seeded data
- Services/ - IAgentService, IKanbanService, IMessageService, ILlmSettingsService
- Components/Pages/ - Home, Kanban, Agents, Messages, Settings pages
- Components/Layout/MainLayout.razor - Radzen layout with sidebar

## Database Seed Data
- 1 default KanbanBoard (id=1) with 5 columns
- 8 BMAD agents (Analyst, PM, Architect, PO, SM, Developer, QA, Orchestrator)
- 4 LLM settings (Copilot, GLM, Claude, LocalLlm)

## Enums
- AgentRole: Analyst, ProductManager, Architect, ProductOwner, ScrumMaster, Developer, QA, Orchestrator
- AgentStatus: Idle, Working, WaitingForInput, Done, Error
- KanbanStatus: Backlog, ToDo, InProgress, Review, Done
- LlmProvider: Copilot, GLM, Claude, LocalLlm

## Radzen Usage
- RadzenLayout/RadzenHeader/RadzenSidebar/RadzenBody for shell
- RadzenDataGrid for tabular data
- RadzenCard for cards
- DialogService for modals
- NotificationService for toasts
- RadzenStack/RadzenRow/RadzenColumn for layout

## EF Core Notes
- AgentMessage.Sender/Receiver use SetNull on delete
- KanbanCard.AssignedAgent uses SetNull on delete
- KanbanColumn/KanbanCard use Cascade on delete
