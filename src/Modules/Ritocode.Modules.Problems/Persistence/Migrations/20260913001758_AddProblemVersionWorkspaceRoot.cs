using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Problems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemVersionWorkspaceRoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "workspace_root",
                schema: "problems",
                table: "problem_versions",
                type: "text",
                nullable: false,
                // Rows written before this column existed get the manifest format's default root.
                // Every package ingested so far declares exactly that root, and the model carries
                // no default, so a new row always states its own.
                defaultValue: "starter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "workspace_root",
                schema: "problems",
                table: "problem_versions");
        }
    }
}
