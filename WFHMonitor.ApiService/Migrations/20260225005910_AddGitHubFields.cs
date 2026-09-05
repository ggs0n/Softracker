using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubBranch",
                table: "ChangeRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubRepoName",
                table: "ChangeRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubRepoOwner",
                table: "ChangeRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubBranch",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "GitHubRepoName",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "GitHubRepoOwner",
                table: "ChangeRequests");
        }
    }
}
