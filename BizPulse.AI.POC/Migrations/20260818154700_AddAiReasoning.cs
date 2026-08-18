using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizPulse.AI.POC.Migrations
{
    public partial class AddAiReasoning : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reasoning",
                table: "ai_agent_executions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ThinkEnabled",
                table: "ai_agent_executions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reasoning",
                table: "ai_agent_executions");

            migrationBuilder.DropColumn(
                name: "ThinkEnabled",
                table: "ai_agent_executions");
        }
    }
}
