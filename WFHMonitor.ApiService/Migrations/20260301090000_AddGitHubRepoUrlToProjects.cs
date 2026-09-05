using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    public partial class AddGitHubRepoUrlToProjects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubRepoUrl",
                table: "ChangeRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubRepoUrl",
                table: "ChangeRequests");
        }
    }
}
