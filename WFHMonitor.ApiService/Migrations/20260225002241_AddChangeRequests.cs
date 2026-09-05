using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    FigmaLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ArchSpecLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ArchSpecNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TimelineStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimelineEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChangeRequestPics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeRequestId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequestPics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeRequestPics_AspNetUsers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChangeRequestPics_ChangeRequests_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalTable: "ChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequestPics_ChangeRequestId",
                table: "ChangeRequestPics",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequestPics_EmployeeId",
                table: "ChangeRequestPics",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_CreatedById",
                table: "ChangeRequests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_CrNumber",
                table: "ChangeRequests",
                column: "CrNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_Status",
                table: "ChangeRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeRequestPics");

            migrationBuilder.DropTable(
                name: "ChangeRequests");
        }
    }
}
