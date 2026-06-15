using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIDevBuddy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsAndMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "KanbanCards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemoryKey",
                table: "AgentMessages",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "AgentMessages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LeadAgentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Agents_LeadAgentId",
                        column: x => x.LeadAgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BmadMemories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedByAgent = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BmadMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BmadMemories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgentAssignments_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectAgentAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_ProjectId",
                table: "KanbanCards",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_ProjectId",
                table: "AgentMessages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BmadMemories_ProjectId",
                table: "BmadMemories",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentAssignments_AgentId",
                table: "ProjectAgentAssignments",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentAssignments_ProjectId",
                table: "ProjectAgentAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_LeadAgentId",
                table: "Projects",
                column: "LeadAgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentMessages_Projects_ProjectId",
                table: "AgentMessages",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KanbanCards_Projects_ProjectId",
                table: "KanbanCards",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentMessages_Projects_ProjectId",
                table: "AgentMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_KanbanCards_Projects_ProjectId",
                table: "KanbanCards");

            migrationBuilder.DropTable(
                name: "BmadMemories");

            migrationBuilder.DropTable(
                name: "ProjectAgentAssignments");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_KanbanCards_ProjectId",
                table: "KanbanCards");

            migrationBuilder.DropIndex(
                name: "IX_AgentMessages_ProjectId",
                table: "AgentMessages");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "MemoryKey",
                table: "AgentMessages");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "AgentMessages");
        }
    }
}
