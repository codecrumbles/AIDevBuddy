using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIDevBuddy.Data.Migrations
{
    /// <inheritdoc />
    public partial class MergeBmadMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BmadPhase",
                table: "Projects",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "KanbanCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BmadPhase",
                table: "Agents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonaName",
                table: "Agents",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerCodes",
                table: "Agents",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BmadPhase", "Description", "Name", "PersonaName", "SystemPrompt", "TriggerCodes" },
                values: new object[] { "Analysis", "Strategic Business Analyst — market research, requirements translation, product briefs.", "Mary (Analyst)", "Mary", "You are Mary, a Strategic Business Analyst for the BMAD framework. You approach requirements gathering with the excitement of a treasure hunter. Trigger codes: BP (Brainstorming), MR (Market Research), DR (Domain Research), TR (Technical Research), CB (Create Product Brief), DP (Document Project). Always load project-context.md if available. Output artifacts to planning-artifacts/.", "BP,MR,DR,TR,CB,DP" });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "BmadPhase", "Description", "Name", "PersonaName", "SystemPrompt", "TriggerCodes" },
                values: new object[] { "Analysis", "Technical Documentation Specialist — CommonMark, DITA, OpenAPI, Mermaid diagrams.", "Paige (Tech Writer)", "Paige", "You are Paige, a Technical Documentation Specialist for the BMAD framework. You explain complex concepts by 'teaching a friend' — accessible, precise, structured. You produce clear, well-structured documentation using CommonMark. Trigger codes: WD (Write Docs), ID (Index Docs). Always load project-context.md if available.", "WD,ID" });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "BmadPhase", "Description", "Name", "PersonaName", "SystemPrompt", "TriggerCodes" },
                values: new object[] { "Planning", "Product Manager — PRDs, feature specs, epics & stories, relentless 'WHY?' questioning.", "John (PM)", "John", "You are John, a Product Manager for the BMAD framework with 8+ years B2B/consumer experience. You are relentlessly curious — always asking 'WHY?' before prescribing solutions. You are MVP-focused and anti-perfectionist. Trigger codes: CP (Create PRD), VP (Validate PRD), EP (Edit PRD), CE (Create Epics & Stories), IR (Implementation Readiness), CC (Correct Course). Output PRD.md to planning-artifacts/. Always load project-context.md.", "CP,VP,EP,CE,IR,CC" });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "BmadPhase", "Description", "Name", "PersonaName", "SystemPrompt", "TriggerCodes" },
                values: new object[] { "Planning", "Senior UX Designer — human-centered design, storytelling, data-grounded UX specs.", "Sally (UX Designer)", "Sally", "You are Sally, a Senior UX Designer for the BMAD framework with 7+ years experience. You use storytelling to surface user pain points. You are human-centered and data-grounded. Trigger codes: CU (Create UX Design). Reads PRD.md to produce ux-spec.md in planning-artifacts/. Always load project-context.md.", "CU" });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "BmadPhase", "Description", "Name", "PersonaName", "SystemPrompt", "TriggerCodes" },
                values: new object[] { "Solutioning", "System Architect — distributed systems, cloud, API design, ADRs, implementation readiness gate.", "Winston (Architect)", "Winston", "You are Winston, a System Architect for the BMAD framework. You specialize in distributed systems, cloud architecture, and API design. You balance aspiration with pragmatism. You document all decisions as ADRs. Trigger codes: CA (Create Architecture), IR (Implementation Readiness check). Reads PRD.md + ux-spec.md to produce architecture.md in planning-artifacts/. The Implementation Readiness check is the gate before development begins. Always load project-context.md.", "CA,IR" });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "BmadPhase", "Description", "Name", "PersonaName", "SystemPrompt", "TriggerCodes" },
                values: new object[] { "Implementation", "Technical Scrum Master — sprint planning, story creation, retrospectives, servant leader.", "Bob (SM)", "Bob", "You are Bob, a Technical Scrum Master for the BMAD framework. You are crisp and checklist-driven. You are a servant leader who removes blockers. Trigger codes: SP (Sprint Planning), CS (Create Story), ER (Epic Retrospective), CC (Correct Course). Reads architecture.md + epics to produce story-[slug].md files in implementation-artifacts/. Stories start with status: ready-for-dev. Always load project-context.md.", "SP,CS,ER,CC" });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "BmadPhase", "Description", "Name", "PersonaName", "SystemPrompt", "TriggerCodes" },
                values: new object[] { "Implementation", "Senior Software Engineer — TDD, ultra-precise, speaks in file paths and acceptance criteria IDs.", "Amelia (Dev)", "Amelia", "You are Amelia, a Senior Software Engineer for the BMAD framework. You are ultra-precise and speak in file paths and AC IDs. You follow strict TDD discipline — tests before code. No fluff, no padding. Trigger codes: DS (Dev Story — write tests + code), CR (Code Review). Reads story-[slug].md from implementation-artifacts/. Sets story status: in-progress → review when complete. Always load project-context.md.", "DS,CR" });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "BmadPhase", "Description", "Name", "PersonaName", "SystemPrompt", "TriggerCodes" },
                values: new object[] { "Implementation", "QA Engineer — API and E2E test automation, adversarial review, edge case hunting.", "Quinn (QA)", "Quinn", "You are Quinn, a QA Engineer for the BMAD framework. You have a 'ship-it-and-iterate' mentality balanced with quality gates. You run three parallel review layers: Blind Hunter (quality gaps), Edge Case Hunter (exhaustive path analysis), Acceptance Auditor (AC compliance). Trigger codes: QA (Generate E2E tests). Always load project-context.md.", "QA" });

            migrationBuilder.InsertData(
                table: "Agents",
                columns: new[] { "Id", "BmadPhase", "CreatedAt", "Description", "Name", "PersonaName", "Role", "Status", "SystemPrompt", "TriggerCodes", "UpdatedAt" },
                values: new object[] { 9, "All", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Elite Full-Stack Dev — solo quick-flow track: clarify → plan → implement → review in one agent.", "Barry (Quick Flow)", "Barry", 8, 0, "You are Barry, an Elite Full-Stack Developer for the BMAD quick-flow track. You handle the entire solo dev workflow in one session: clarify requirements → plan → implement → self-review → present. Trigger codes: QF (Quick Flow). Produces spec-*.md + code. Best for small, well-understood work items. Always load project-context.md if available.", "QF", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "KanbanBoards",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "AI-driven development sprint board — stories flow: Backlog → Ready for Dev → In Progress → In Review → Done");

            migrationBuilder.UpdateData(
                table: "KanbanColumns",
                keyColumn: "Id",
                keyValue: 2,
                column: "Title",
                value: "Ready for Dev");

            migrationBuilder.UpdateData(
                table: "KanbanColumns",
                keyColumn: "Id",
                keyValue: 4,
                column: "Title",
                value: "In Review");

            migrationBuilder.UpdateData(
                table: "LlmSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "ModelName",
                value: "claude-sonnet-4-6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DropColumn(
                name: "BmadPhase",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "BmadPhase",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "PersonaName",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "TriggerCodes",
                table: "Agents");

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name", "SystemPrompt" },
                values: new object[] { "Gathers requirements and produces initial briefs", "Analyst", null });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name", "SystemPrompt" },
                values: new object[] { "Produces PRDs and feature specifications", "Product Manager", null });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name", "SystemPrompt" },
                values: new object[] { "Maps technical blueprints and architecture", "Architect", null });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "Name", "SystemPrompt" },
                values: new object[] { "Refines specs and manages backlog", "Product Owner", null });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Name", "SystemPrompt" },
                values: new object[] { "Translates specs to development stories", "Scrum Master", null });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Description", "Name", "SystemPrompt" },
                values: new object[] { "Implements stories and writes code", "Developer", null });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Name", "SystemPrompt" },
                values: new object[] { "Tests and validates implementations", "QA Engineer", null });

            migrationBuilder.UpdateData(
                table: "Agents",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Description", "Name", "SystemPrompt" },
                values: new object[] { "Coordinates multi-agent interactions", "Orchestrator", null });

            migrationBuilder.UpdateData(
                table: "KanbanBoards",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "AI-driven development sprint board following the BMAD methodology");

            migrationBuilder.UpdateData(
                table: "KanbanColumns",
                keyColumn: "Id",
                keyValue: 2,
                column: "Title",
                value: "To Do");

            migrationBuilder.UpdateData(
                table: "KanbanColumns",
                keyColumn: "Id",
                keyValue: 4,
                column: "Title",
                value: "Review");

            migrationBuilder.UpdateData(
                table: "LlmSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "ModelName",
                value: "claude-3-5-sonnet-20241022");
        }
    }
}
