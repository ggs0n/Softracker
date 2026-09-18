using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectKickStartDesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These Codex fields predate formal migrations and may already exist in
            // a user's local database. Keep this migration safe for both upgraded
            // and newly created installations.
            migrationBuilder.Sql("""
                IF COL_LENGTH('SystemPreferences', 'CodexModel') IS NULL
                    ALTER TABLE [SystemPreferences] ADD [CodexModel] nvarchar(100) NOT NULL CONSTRAINT [DF_SystemPreferences_CodexModel] DEFAULT N'gpt-5.6-sol';
                IF COL_LENGTH('SystemPreferences', 'CodexReasoningEffort') IS NULL
                    ALTER TABLE [SystemPreferences] ADD [CodexReasoningEffort] nvarchar(20) NOT NULL CONSTRAINT [DF_SystemPreferences_CodexReasoningEffort] DEFAULT N'low';
                IF COL_LENGTH('ChangeRequests', 'CodeReadinessScanAgentId') IS NULL
                    ALTER TABLE [ChangeRequests] ADD [CodeReadinessScanAgentId] nvarchar(100) NULL;
                IF COL_LENGTH('ChangeRequests', 'CodeReadinessScanCommitSha') IS NULL
                    ALTER TABLE [ChangeRequests] ADD [CodeReadinessScanCommitSha] nvarchar(64) NULL;
                IF COL_LENGTH('ChangeRequests', 'CodeReadinessScanCompletedAt') IS NULL
                    ALTER TABLE [ChangeRequests] ADD [CodeReadinessScanCompletedAt] datetime2 NULL;
                IF COL_LENGTH('ChangeRequests', 'CodeReadinessScanMessage') IS NULL
                    ALTER TABLE [ChangeRequests] ADD [CodeReadinessScanMessage] nvarchar(500) NULL;
                IF COL_LENGTH('ChangeRequests', 'CodeReadinessScanResultJson') IS NULL
                    ALTER TABLE [ChangeRequests] ADD [CodeReadinessScanResultJson] nvarchar(max) NULL;
                IF COL_LENGTH('ChangeRequests', 'CodeReadinessScanStartedAt') IS NULL
                    ALTER TABLE [ChangeRequests] ADD [CodeReadinessScanStartedAt] datetime2 NULL;
                IF COL_LENGTH('ChangeRequests', 'CodeReadinessScanStatus') IS NULL
                    ALTER TABLE [ChangeRequests] ADD [CodeReadinessScanStatus] nvarchar(20) NOT NULL CONSTRAINT [DF_ChangeRequests_CodeReadinessScanStatus] DEFAULT N'None';
                """);

            migrationBuilder.CreateTable(
                name: "ProjectKickStartDesigns",
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
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectKickStartDesigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectKickStartDesigns_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChangeRequests_CodeReadinessScanStatus' AND object_id = OBJECT_ID(N'[ChangeRequests]'))
                    CREATE INDEX [IX_ChangeRequests_CodeReadinessScanStatus] ON [ChangeRequests] ([CodeReadinessScanStatus]);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectKickStartDesigns_CreatedById_CreatedAtUtc",
                table: "ProjectKickStartDesigns",
                columns: new[] { "CreatedById", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectKickStartDesigns");

            // Do not remove the older Codex columns here; upgraded databases may
            // have created them before EF began tracking this migration.
        }
    }
}
