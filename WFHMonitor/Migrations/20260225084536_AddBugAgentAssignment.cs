using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddBugAgentAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgentStatus",
                table: "BugReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AssigneeType",
                table: "BugReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_AgentStatus",
                table: "BugReports",
                column: "AgentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_AssigneeType",
                table: "BugReports",
                column: "AssigneeType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BugReports_AgentStatus",
                table: "BugReports");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_AssigneeType",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "AgentStatus",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "AssigneeType",
                table: "BugReports");
        }
    }
}
