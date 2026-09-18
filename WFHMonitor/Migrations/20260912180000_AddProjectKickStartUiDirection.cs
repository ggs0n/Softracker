using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WFHMonitor.Data;

#nullable disable

namespace WFHMonitor.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912180000_AddProjectKickStartUiDirection")]
public partial class AddProjectKickStartUiDirection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "UiDirection",
            table: "ProjectKickStartDesigns",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UiDirection",
            table: "ProjectKickStartDesigns");
    }
}
