using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WFHMonitor.Data;

#nullable disable

namespace WFHMonitor.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912191000_RemoveOpenAiImageSettings")]
public partial class RemoveOpenAiImageSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OpenAiImageApiKeyProtected", table: "SystemPreferences");
        migrationBuilder.DropColumn(name: "OpenAiImageModel", table: "SystemPreferences");
        migrationBuilder.DropColumn(name: "OpenAiImageQuality", table: "SystemPreferences");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OpenAiImageApiKeyProtected",
            table: "SystemPreferences",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OpenAiImageModel",
            table: "SystemPreferences",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "gpt-image-2.5-flare");

        migrationBuilder.AddColumn<string>(
            name: "OpenAiImageQuality",
            table: "SystemPreferences",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "medium");
    }
}
