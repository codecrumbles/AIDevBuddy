using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIDevBuddy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAutoLoop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoLoop",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AutoLoopIntervalSeconds",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "AutoLoopStatus",
                table: "Projects",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Idle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AutoLoop",               table: "Projects");
            migrationBuilder.DropColumn(name: "AutoLoopIntervalSeconds", table: "Projects");
            migrationBuilder.DropColumn(name: "AutoLoopStatus",          table: "Projects");
        }
    }
}
