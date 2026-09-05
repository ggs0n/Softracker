using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddBrainstormDesignProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrainstormDesignProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Technology = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CloudHostingTarget = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UserCount = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Features = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlueprintJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceMode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrainstormDesignProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrainstormDesignProjects_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrainstormDesignProjects_CreatedById",
                table: "BrainstormDesignProjects",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_BrainstormDesignProjects_CreatedById_CreatedAt",
                table: "BrainstormDesignProjects",
                columns: new[] { "CreatedById", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrainstormDesignProjects");
        }
    }
}
