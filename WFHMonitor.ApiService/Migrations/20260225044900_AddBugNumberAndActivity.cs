using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddBugNumberAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BugNumber",
                table: "BugReports",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BugActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BugReportId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: true),
                    OldAssignedDeveloperId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    NewAssignedDeveloperId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BugActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BugActivities_AspNetUsers_NewAssignedDeveloperId",
                        column: x => x.NewAssignedDeveloperId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BugActivities_AspNetUsers_OldAssignedDeveloperId",
                        column: x => x.OldAssignedDeveloperId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BugActivities_BugReports_BugReportId",
                        column: x => x.BugReportId,
                        principalTable: "BugReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_BugNumber",
                table: "BugReports",
                column: "BugNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BugActivities_BugReportId",
                table: "BugActivities",
                column: "BugReportId");

            migrationBuilder.CreateIndex(
                name: "IX_BugActivities_CreatedAt",
                table: "BugActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BugActivities_NewAssignedDeveloperId",
                table: "BugActivities",
                column: "NewAssignedDeveloperId");

            migrationBuilder.CreateIndex(
                name: "IX_BugActivities_OldAssignedDeveloperId",
                table: "BugActivities",
                column: "OldAssignedDeveloperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BugActivities");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_BugNumber",
                table: "BugReports");

            migrationBuilder.DropColumn(
                name: "BugNumber",
                table: "BugReports");
        }
    }
}
