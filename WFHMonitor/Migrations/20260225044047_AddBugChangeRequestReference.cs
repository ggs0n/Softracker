using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddBugChangeRequestReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChangeRequestId",
                table: "BugReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeRequestReferenceText",
                table: "BugReports",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_ChangeRequestId",
                table: "BugReports",
                column: "ChangeRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_BugReports_ChangeRequests_ChangeRequestId",
                table: "BugReports",
                column: "ChangeRequestId",
                principalTable: "ChangeRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BugReports_ChangeRequests_ChangeRequestId",
                table: "BugReports");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_ChangeRequestId",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "ChangeRequestId",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "ChangeRequestReferenceText",
                table: "BugReports");
        }
    }
}
