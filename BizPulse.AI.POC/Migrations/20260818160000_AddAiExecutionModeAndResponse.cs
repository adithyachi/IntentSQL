using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizPulse.AI.POC.Migrations
{
    public partial class AddAiExecutionModeAndResponse : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "ai_agent_executions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Strict SQL");

            migrationBuilder.AddColumn<string>(
                name: "Response",
                table: "ai_agent_executions",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                table: "ai_agent_executions");

            migrationBuilder.DropColumn(
                name: "Response",
                table: "ai_agent_executions");
        }
    }
}
