using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIDevBuddy.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixOllamaEndpointSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "LlmSettings",
                keyColumn: "Id",
                keyValue: 4,
                column: "Endpoint",
                value: "http://localhost:11434");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "LlmSettings",
                keyColumn: "Id",
                keyValue: 4,
                column: "Endpoint",
                value: "http://localhost:11434/api/");
        }
    }
}
